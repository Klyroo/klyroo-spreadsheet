using Microsoft.Maui.Controls.Shapes;

namespace Klyroo.Spreadsheet.Maui;

/// <summary>Small building blocks shared by the steps of the wizard.</summary>
internal static class SheetUi
{
    private const float Tint = 0.10f;
    private const float StrongTint = 0.15f;

    public static void SetThemeColor(this BindableObject target, BindableProperty property, KlyThemeColor color) =>
        target.SetAppThemeColor(property, color.Light, color.Dark);

    public static void SetStroke(this Border border, KlyThemeColor color) =>
        border.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(color.Light), new SolidColorBrush(color.Dark));

    public static void SetRadius(this Border border, double radius) =>
        border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) };

    /// <summary>The corner radius of a fraction of <see cref="KlyTheme.Radius"/>, as <c>rounded-(--klyroo-radius)</c> and its variants on the web.</summary>
    public static double RadiusOf(double fraction) => KlyTheme.Radius * fraction;

    /// <summary>The text color of a semantic color on the page background.</summary>
    public static KlyThemeColor TextOf(KlyColor color) => KlyTheme.Resolve(color).Text;

    /// <summary>A light tint of a semantic color, as <c>bg-(--klyroo-success)/15</c> on the web.</summary>
    public static KlyThemeColor TintOf(KlyColor color, bool strong = false) => KlyTheme.Resolve(color).Fill.WithAlpha(strong ? StrongTint : Tint);

    public static Label NewLabel(double size, KlyThemeColor color, FontAttributes attributes = FontAttributes.None, LineBreakMode lineBreak = LineBreakMode.WordWrap)
    {
        var label = new Label { FontSize = size, FontAttributes = attributes, LineBreakMode = lineBreak };
        label.SetThemeColor(Label.TextColorProperty, color);
        return label;
    }

    public static Label Text(string? text, double size, KlyThemeColor color, FontAttributes attributes = FontAttributes.None, LineBreakMode lineBreak = LineBreakMode.WordWrap)
    {
        var label = NewLabel(size, color, attributes, lineBreak);
        label.Text = text;
        return label;
    }

    /// <summary>Calls <paramref name="action"/> when the view is tapped while it is enabled.</summary>
    public static void Tap(View view, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (view.IsEnabled) action();
        };
        view.GestureRecognizers.Add(tap);
    }

    /// <summary>Runs an action on the UI thread: at once when the caller already is on it, or when no dispatcher can be found (unit tests).</summary>
    public static void Post(BindableObject owner, Action action)
    {
        IDispatcher? dispatcher = null;
        try
        {
            dispatcher = (owner as Element)?.Dispatcher;
        }
        catch (Exception)
        {
            // No application, no dispatcher: run inline.
        }
        if (dispatcher is { IsDispatchRequired: true }) dispatcher.Dispatch(action);
        else action();
    }

    /// <summary>A button of the wizard: a <see cref="KlyButton"/> that is at least 40 units high, as a touch target must be.</summary>
    public static KlyButton Button(string? text, ButtonVariant variant, KlyColor color, Action onClick, KlySize size = KlySize.Sm)
    {
        var button = new KlyButton { Text = text, Variant = variant, Color = color, Size = size, MinimumHeightRequest = 40 };
        button.Clicked += (_, _) => onClick();
        return button;
    }

    /// <summary>A link-like action: colored text with no frame.</summary>
    public static KlyButton Link(string? text, Action onClick, KlyColor? color = null) =>
        Button(text, ButtonVariant.Link, color ?? KlyColor.Primary, onClick);

    /// <summary>A rounded label on a tinted background, such as the status of a row or the pill of a file name.</summary>
    public static Border Pill(string? text, KlyThemeColor fill, KlyThemeColor textColor, double size = 12, FontAttributes attributes = FontAttributes.Bold)
    {
        var pill = new Border { StrokeThickness = 0, Padding = new Thickness(10, 3), HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center };
        pill.SetRadius(999);
        pill.SetThemeColor(VisualElement.BackgroundColorProperty, fill);
        pill.Content = Text(text, size, textColor, attributes, LineBreakMode.NoWrap);
        return pill;
    }

    /// <summary>A bordered box, as the cards of the web wizard.</summary>
    public static Border Card(View content, double padding = 12, KlyThemeColor? fill = null)
    {
        var card = new Border { StrokeThickness = 1, Padding = new Thickness(padding), Content = content, HorizontalOptions = LayoutOptions.Fill };
        card.SetRadius(RadiusOf(1));
        card.SetStroke(KlyTheme.Border);
        if (fill is { } color) card.SetThemeColor(VisualElement.BackgroundColorProperty, color);
        return card;
    }

    /// <summary>A message on a tinted background in the color of its tone: an error, a warning or a success.</summary>
    public static Border Notice(string? text, KlyColor tone, bool announce = true)
    {
        var box = new Border { StrokeThickness = 0, Padding = new Thickness(12, 8), HorizontalOptions = LayoutOptions.Fill };
        box.SetRadius(RadiusOf(1));
        box.SetThemeColor(VisualElement.BackgroundColorProperty, TintOf(tone, strong: tone != KlyColor.Error));
        box.Content = Text(text, 12, TextOf(tone));
        SemanticProperties.SetDescription(box, text);
        if (!announce) AutomationProperties.SetIsInAccessibleTree(box, false);
        return box;
    }

    /// <summary>A select of the wizard over <see cref="SheetChoice"/> entries. The handler is attached after the selection is set, so it only hears the user.</summary>
    public static KlySelect Select(IReadOnlyList<SheetChoice> choices, SheetChoice? selected, string accessibleName, Action<SheetChoice> onChanged, bool enabled = true)
    {
        var select = new KlySelect { ItemsSource = choices.ToList(), SelectedItem = selected, AccessibleName = accessibleName, Size = KlySize.Sm, IsEnabled = enabled };
        select.SelectedItemChanged += (_, _) =>
        {
            if (select.SelectedItem is SheetChoice choice && !Equals(choice, selected)) onChanged(choice);
        };
        return select;
    }

    /// <summary>The extension of a file name, upper case, for the badge of the loaded file: <c>CSV</c> when it has none.</summary>
    public static string ExtensionLabel(string? fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName)?.TrimStart('.').ToUpperInvariant();
        return string.IsNullOrEmpty(ext) ? "CSV" : ext;
    }
}
