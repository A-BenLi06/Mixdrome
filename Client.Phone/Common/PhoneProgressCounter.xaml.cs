namespace Mixdrome.Phone.Common
{
    using System;
    using System.Globalization;
    using Windows.Foundation;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;

    // Both reference surfaces render the current track's position, with a fixed
    // seconds column. Their negative margins belong to the two-column layout.
    public sealed partial class PhoneProgressCounter : UserControl
    {
        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register("Position", typeof(TimeSpan), typeof(PhoneProgressCounter), new PropertyMetadata(TimeSpan.Zero, OnPresentationChanged));
        public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register("Scale", typeof(double), typeof(PhoneProgressCounter), new PropertyMetadata(1d, OnPresentationChanged));
        public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register("IsCompact", typeof(bool), typeof(PhoneProgressCounter), new PropertyMetadata(false, OnPresentationChanged));
        public TimeSpan Position { get { return (TimeSpan)GetValue(PositionProperty); } set { SetValue(PositionProperty, value); } }
        public double Scale { get { return (double)GetValue(ScaleProperty); } set { SetValue(ScaleProperty, value); } }
        public bool IsCompact { get { return (bool)GetValue(IsCompactProperty); } set { SetValue(IsCompactProperty, value); } }

        public PhoneProgressCounter() { InitializeComponent(); UpdatePresentation(); }
        private static void OnPresentationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) { ((PhoneProgressCounter)sender).UpdatePresentation(); }
        private void UpdatePresentation()
        {
            if (Digits == null) return;
            var scale = Scale;
            if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) return;
            var position = Position < TimeSpan.Zero ? TimeSpan.Zero : Position;
            var longTime = position.TotalHours >= 1;
            var shortTime = !longTime && position.Minutes <= 9;
            MinutesText.Text = longTime
                ? ((long)position.TotalHours).ToString(CultureInfo.CurrentCulture) + position.ToString("\\:mm", CultureInfo.CurrentCulture)
                : position.ToString("%m", CultureInfo.CurrentCulture);
            SecondsText.Text = position.ToString("\\:ss", CultureInfo.CurrentCulture);
            double fontSize, secondsWidth;
            Thickness margin;
            if (IsCompact)
            {
                fontSize = longTime ? 78 : 91;
                secondsWidth = longTime ? 108 : shortTime ? 174 : 132;
                Height = 60 * scale;
                VerticalAlignment = VerticalAlignment.Center;
                Digits.VerticalAlignment = VerticalAlignment.Top;
                margin = new Thickness(0, (longTime ? -13 : -19) * scale, (longTime ? 0 : shortTime ? -50 : -8) * scale, 0);
                Clip = new RectangleGeometry { Rect = new Rect(0, 0, 200 * scale, 60 * scale) };
            }
            else
            {
                fontSize = longTime ? 68 : shortTime ? 124 : 94;
                secondsWidth = longTime ? 96 : shortTime ? 174 : 132;
                Height = double.NaN;
                VerticalAlignment = VerticalAlignment.Bottom;
                Digits.VerticalAlignment = VerticalAlignment.Stretch;
                margin = new Thickness((longTime ? -20 : shortTime ? -100 : -60) * scale, 0, 0, (longTime ? -18 : shortTime ? -34 : -26) * scale);
                Clip = null;
            }
            Digits.Margin = margin;
            SecondsColumn.Width = new GridLength(secondsWidth * scale);
            MinutesText.FontSize = SecondsText.FontSize = fontSize * scale;
            MinutesText.Opacity = SecondsText.Opacity = IsCompact ? 0.1 : 0.7;
            MinutesText.VerticalAlignment = IsCompact && shortTime ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        }
    }
}
