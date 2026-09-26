using System.Windows.Data;
using System.Windows.Markup;

namespace ScreenFilter.Services;

[MarkupExtensionReturnType(typeof(string))]
public class LocExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
