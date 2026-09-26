using System.Runtime.InteropServices;
using ScreenFilter.Models;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using static ScreenFilter.Services.Native;

namespace ScreenFilter.Services;

public sealed class CaptureEngine : IDisposable
{
    private static readonly Guid IID_GraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IID_Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    private static readonly Guid IID_GraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

    private readonly object _gate = new();
    private readonly bool _windowMode;
    private readonly IntPtr _targetWindow;
    private readonly bool _onlyForeground;
    private readonly IntPtr _selfWindow;

    private ID3D11Device _device = null!;
    private ID3D11DeviceContext _ctx = null!;
    private IDXGISwapChain1 _swap = null!;
    private ID3D11RenderTargetView? _rtv;
    private ID3D11Texture2D? _frameCopy;
    private ID3D11ShaderResourceView? _srv;
    private ID3D11VertexShader _vs = null!;
    private ID3D11PixelShader _ps = null!;
    private ID3D11SamplerState _sampler = null!;
    private ID3D11Buffer _cb = null!;
    private IDirect3DDevice _winrtDevice = null!;
    private GraphicsCaptureItem _item = null!;
    private Direct3D11CaptureFramePool _pool = null!;
    private GraphicsCaptureSession _session = null!;
    private OverlayWindow _overlay = null!;
    private Timer? _followTimer;
    private Windows.Graphics.SizeInt32 _poolSize;
    private int _width, _height;
    private bool _disposed;
    private bool _hasFrame;
    private string? _snapshotPath;
    private readonly ManualResetEventSlim _snapshotDone = new();

    private volatile float[] _constants = new float[28];
    private long _frameCount;

    public long FrameCount => Interlocked.Read(ref _frameCount);
    public event Action? TargetClosed;

    public CaptureEngine(MonitorSource? monitor, WindowSource? window, FilterSettings initial, bool onlyForeground, bool excludeFromCapture, IntPtr selfWindow, int maxFps)
    {
        _maxFps = maxFps;
        _displayDevice = monitor?.Device;
        UpdateSettings(initial, redraw: false);
        _windowMode = window != null;
        _targetWindow = window?.Handle ?? IntPtr.Zero;
        _onlyForeground = onlyForeground && _windowMode;
        _selfWindow = selfWindow;

        RECT bounds = _windowMode ? GetVisibleBounds(_targetWindow) : monitor!.Bounds;
        _width = Math.Max(bounds.Width, 16);
        _height = Math.Max(bounds.Height, 16);

        // Overlay must be excluded from capture when capturing a monitor, otherwise it would capture itself.
        _overlay = new OverlayWindow(bounds, excludeFromCapture || !_windowMode);

        try
        {
            CreateDevice();
            CreateShaders();
            CreateSwapChain();
            CreateCapture(monitor, window);
        }
        catch
        {
            Dispose();
            throw;
        }

        _overlay.SetVisible(true);
        if (_windowMode) _followTimer = new Timer(_ => Follow(), null, 0, 8);
    }

    /// <summary>Re-draws the last captured frame (a static screen delivers no new frames, so setting changes need this).</summary>
    public void Redraw()
    {
        lock (_gate)
        {
            if (_disposed || !_hasFrame) return;
            try { Render(); } catch (Exception ex) { Failed?.Invoke(ex.Message); }
        }
    }

    public void UpdateSettings(FilterSettings s, bool redraw = true)
    {
        var m = ColorMath.Build(s);
        float target = s.TargetHue / 360f;
        _constants =
        [
            m.A[0], m.A[1], m.A[2], m.B[0],
            m.A[3], m.A[4], m.A[5], m.B[1],
            m.A[6], m.A[7], m.A[8], m.B[2],
            s.Gamma, s.ShadowLift, s.Highlights, s.Vibrance,
            s.Sharpen, s.Clarity, s.Dehaze, s.TargetAmount,
            target, Math.Clamp(s.TargetRange / 360f, 0.005f, 0.5f), 0f, 0f,
            s.DarkBoost, 0f, 0f, 0f,
        ];
        if (redraw) Redraw();
    }

    private void CreateDevice()
    {
        var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0], out _device!, out _ctx!);
        result.CheckError();

        using var dxgi = _device.QueryInterface<IDXGIDevice>();
        try { dxgi.SetGPUThreadPriority(7); using var d1 = _device.QueryInterface<IDXGIDevice1>(); d1.MaximumFrameLatency = 1; } catch { }
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var inspectable));
        try { _winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable); }
        finally { Marshal.Release(inspectable); }
    }

    private void CreateShaders()
    {
        _vs = _device.CreateVertexShader(Compile("VS", "vs_5_0"));
        _ps = _device.CreatePixelShader(Compile("PS", "ps_5_0"));
        _sampler = _device.CreateSamplerState(new SamplerDescription(Filter.MinMagMipLinear,
            TextureAddressMode.Clamp, TextureAddressMode.Clamp, TextureAddressMode.Clamp));
        _cb = _device.CreateBuffer(new BufferDescription(28 * sizeof(float), BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));
    }

    private static byte[] Compile(string entry, string profile)
    {
        var hr = Compiler.Compile(Shaders.Source, entry, "filter.hlsl", profile, out var blob, out var err);
        if (hr.Failure || blob == null)
            throw new InvalidOperationException("Shader compile failed: " + (err != null ? System.Text.Encoding.UTF8.GetString(err.AsBytes()) : hr.ToString()));
        var bytes = blob.AsBytes();
        blob.Dispose();
        err?.Dispose();
        return bytes;
    }

    private void CreateSwapChain()
    {
        using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();
        var desc = new SwapChainDescription1
        {
            Width = (uint)_width,
            Height = (uint)_height,
            Format = Format.B8G8R8A8_UNorm,
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
        };
        _swap = factory.CreateSwapChainForHwnd(_device, _overlay.Handle, desc, null, null);
        factory.MakeWindowAssociation(_overlay.Handle, WindowAssociationFlags.IgnoreAll);
        CreateTargets();
    }

    private void CreateTargets()
    {
        _rtv?.Dispose();
        _frameCopy?.Dispose();
        _srv?.Dispose();

        using (var back = _swap.GetBuffer<ID3D11Texture2D>(0))
            _rtv = _device.CreateRenderTargetView(back);

        _frameCopy = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)_width,
            Height = (uint)_height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource,
        });
        _srv = _device.CreateShaderResourceView(_frameCopy);
    }

    private void CreateCapture(MonitorSource? monitor, WindowSource? window)
    {
        _item = CreateItem(window?.Handle, monitor?.Handle);
        _poolSize = _item.Size;
        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 3, _poolSize);
        _pool.FrameArrived += OnFrameArrived;
        _item.Closed += (_, _) => TargetClosed?.Invoke();
        _session = _pool.CreateCaptureSession(_item);
        try { _session.IsCursorCaptureEnabled = true; } catch { }
        try { _session.IsBorderRequired = false; } catch { }
        SetMaxFps(_maxFps);
        _session.StartCapture();
    }

    private static GraphicsCaptureItem CreateItem(IntPtr? hwnd, IntPtr? hmon)
    {
        var hstr = IntPtr.Zero;
        Marshal.ThrowExceptionForHR(WindowsCreateString("Windows.Graphics.Capture.GraphicsCaptureItem", 44, out hstr));
        IntPtr factory;
        try { Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstr, IID_GraphicsCaptureItemInterop, out factory)); }
        finally { WindowsDeleteString(hstr); }

        try
        {
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
            var ptr = hwnd.HasValue ? interop.CreateForWindow(hwnd.Value, IID_GraphicsCaptureItem)
                                    : interop.CreateForMonitor(hmon!.Value, IID_GraphicsCaptureItem);
            try { return MarshalInspectable<GraphicsCaptureItem>.FromAbi(ptr); }
            finally { Marshal.Release(ptr); }
        }
        finally { Marshal.Release(factory); }
    }

    private int _errors;
    public event Action<string>? Failed;
    private int _maxFps;
    private string? _displayDevice;

    /// <summary>0 = display refresh rate, otherwise the cap in frames per second.</summary>
    public void SetMaxFps(int fps)
    {
        _maxFps = fps;
        double display = DisplayInfo.RefreshRate(_displayDevice);
        double target = fps <= 0 ? display : fps >= 1000 ? 100000 : Math.Min(fps, display);
        double ticks = Math.Max(1, Math.Round(display / target));
        double ms = fps >= 1000 ? 0.5 : (ticks - 0.5) * 1000.0 / display;
        try { _session.MinUpdateInterval = TimeSpan.FromMilliseconds(ms); } catch { }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try { OnFrameArrivedCore(sender); _errors = 0; }
        catch (Exception ex) when (!_disposed)
        {
            if (++_errors >= 20) Failed?.Invoke(ex.Message);
        }
    }

    private void OnFrameArrivedCore(Direct3D11CaptureFramePool sender)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;

        lock (_gate)
        {
            if (_disposed) return;

            var size = frame.ContentSize;
            if (size.Width != _poolSize.Width || size.Height != _poolSize.Height)
            {
                _poolSize = size;
                sender.Recreate(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 3, size);
                return;
            }

            if (size.Width != _width || size.Height != _height)
            {
                _width = Math.Max(size.Width, 16);
                _height = Math.Max(size.Height, 16);
                _rtv?.Dispose(); _rtv = null;
                _swap.ResizeBuffers(0, (uint)_width, (uint)_height, Format.Unknown, SwapChainFlags.None);
                CreateTargets();
            }

            using var tex = GetTexture(frame.Surface);
            _ctx.CopySubresourceRegion(_frameCopy!, 0, 0, 0, 0, tex, 0,
                new Box(0, 0, 0, Math.Min(_width, (int)tex.Description.Width), Math.Min(_height, (int)tex.Description.Height), 1));

            _hasFrame = true;
            Render();
            Interlocked.Increment(ref _frameCount);
        }
    }

    private static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var abi = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(abi, typeof(Native.IDirect3DDxgiInterfaceAccess).GUID, out var access));
            try
            {
                var obj = (Native.IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(access);
                return new ID3D11Texture2D(obj.GetInterface(IID_Texture2D));
            }
            finally { Marshal.Release(access); }
        }
        finally { Marshal.Release(abi); }
    }

    private void Render()
    {
        var mapped = _ctx.Map(_cb, MapMode.WriteDiscard);
        var c = (float[])_constants.Clone();
        c[22] = 1f / _width;
        c[23] = 1f / _height;
        Marshal.Copy(c, 0, mapped.DataPointer, c.Length);
        _ctx.Unmap(_cb, 0);

        _ctx.OMSetRenderTargets(_rtv!);
        _ctx.RSSetViewport(0, 0, _width, _height);
        _ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _ctx.VSSetShader(_vs);
        _ctx.PSSetShader(_ps);
        _ctx.PSSetConstantBuffer(0, _cb);
        _ctx.PSSetShaderResource(0, _srv);
        _ctx.PSSetSampler(0, _sampler);
        _ctx.Draw(3, 0);

        if (_snapshotPath != null) TakeSnapshot();

        _swap.Present(0, PresentFlags.None);
    }

    private void TakeSnapshot()
    {
        var path = _snapshotPath!;
        _snapshotPath = null;
        using var back = _swap.GetBuffer<ID3D11Texture2D>(0);
        var desc = back.Description;
        desc.Usage = ResourceUsage.Staging;
        desc.BindFlags = BindFlags.None;
        desc.CPUAccessFlags = CpuAccessFlags.Read;
        desc.MiscFlags = ResourceOptionFlags.None;
        using var staging = _device.CreateTexture2D(desc);
        _ctx.CopyResource(staging, back);
        var map = _ctx.Map(staging, 0, MapMode.Read);
        try
        {
            int w = (int)desc.Width, h = (int)desc.Height;
            var pixels = new byte[w * 4 * h];
            for (int y = 0; y < h; y++)
                Marshal.Copy(map.DataPointer + y * (int)map.RowPitch, pixels, y * w * 4, w * 4);
            var bmp = System.Windows.Media.Imaging.BitmapSource.Create(w, h, 96, 96,
                System.Windows.Media.PixelFormats.Bgra32, null, pixels, w * 4);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var fs = System.IO.File.Create(path);
            enc.Save(fs);
        }
        finally { _ctx.Unmap(staging, 0); }
        _snapshotDone.Set();
    }

    /// <summary>Saves the next filtered frame as PNG (used by the self test).</summary>
    public bool SaveSnapshot(string path, int timeoutMs = 5000)
    {
        _snapshotDone.Reset();
        _snapshotPath = path;
        Redraw();
        return _snapshotDone.Wait(timeoutMs);
    }

    private void Follow()
    {
        if (_disposed) return;
        try
        {
            if (!IsWindow(_targetWindow)) { TargetClosed?.Invoke(); return; }
            bool minimized = IsIconic(_targetWindow);
            bool visible = !minimized && IsWindowVisible(_targetWindow);
            if (visible && _onlyForeground)
            {
                var fg = GetForegroundWindow();
                visible = fg == _targetWindow || fg == _selfWindow || fg == _overlay.Handle;
            }
            if (visible)
            {
                var r = GetVisibleBounds(_targetWindow);
                if (r.Width > 0 && r.Height > 0) _overlay.SetBounds(r);
            }
            _overlay.SetVisible(visible);
        }
        catch { }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _followTimer?.Dispose();
        try { _session?.Dispose(); } catch { }
        try { if (_pool != null) { _pool.FrameArrived -= OnFrameArrived; _pool.Dispose(); } } catch { }
        lock (_gate)
        {
            _rtv?.Dispose();
            _srv?.Dispose();
            _frameCopy?.Dispose();
            _cb?.Dispose();
            _sampler?.Dispose();
            _ps?.Dispose();
            _vs?.Dispose();
            _swap?.Dispose();
            _ctx?.Dispose();
            _device?.Dispose();
        }
        _overlay?.Dispose();
    }
}
