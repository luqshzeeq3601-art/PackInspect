using System.Windows;
using System.Windows.Controls;

namespace PackInspect.App.Views;

/// <summary>
/// Lets a <see cref="PasswordBox"/> be bound to a view model string. WPF does not
/// allow binding <c>PasswordBox.Password</c> directly, and a plain text box would
/// show the password on screen. This lives in the view layer, so the view models
/// still know nothing about WPF controls.
/// </summary>
public static class PasswordBoxBinding
{
    // Null by default, so the first value from the view model (an empty string)
    // counts as a change and attaches the handler below.
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
        "Password",
        typeof(string),
        typeof(PasswordBoxBinding),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged));

    public static string? GetPassword(DependencyObject element) => (string?)element.GetValue(PasswordProperty);

    public static void SetPassword(DependencyObject element, string? value) => element.SetValue(PasswordProperty, value);

    // View model -> box, for example when the view model clears the password after a login.
    private static void OnPasswordPropertyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= OnPasswordBoxChanged;
        string password = (string?)e.NewValue ?? string.Empty;
        if (box.Password != password)
        {
            box.Password = password;
        }

        box.PasswordChanged += OnPasswordBoxChanged;
    }

    // Box -> view model, on every keystroke.
    private static void OnPasswordBoxChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetPassword(box, box.Password);
    }
}
