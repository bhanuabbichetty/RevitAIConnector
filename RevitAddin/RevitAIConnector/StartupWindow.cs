using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RevitAIConnector
{
    public class StartupWindow : Window
    {
        private readonly DispatcherTimer _autoCloseTimer;

        public StartupWindow(int port, int toolCount, bool isError = false, string errorMsg = null)
        {
            Title = "Revit AI Connector";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 32)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 24, ShadowDepth = 4, Opacity = 0.5
                },
                Child = BuildContent(port, toolCount, isError, errorMsg)
            };

            Content = card;

            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _autoCloseTimer.Tick += (s, e) => { _autoCloseTimer.Stop(); Close(); };
            if (!isError) _autoCloseTimer.Start();

            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
        }

        private UIElement BuildContent(int port, int toolCount, bool isError, string errorMsg)
        {
            var stack = new StackPanel { Margin = new Thickness(0) };

            // ── Header bar with gradient ────────────────────────────────
            var headerBorder = new Border
            {
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Padding = new Thickness(24, 18, 24, 18),
                Background = new LinearGradientBrush(
                    Color.FromRgb(56, 132, 232),
                    Color.FromRgb(104, 72, 232),
                    45)
            };

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            headerStack.Children.Add(new TextBlock
            {
                Text = "\uE99A",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 22,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Revit AI Connector",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            var closeBtn = new Button
            {
                Content = "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(6),
                Margin = new Thickness(0, -4, -8, 0)
            };
            closeBtn.Click += (s, e) => Close();

            var headerGrid = new Grid();
            headerGrid.Children.Add(headerStack);
            headerGrid.Children.Add(closeBtn);
            headerBorder.Child = headerGrid;
            stack.Children.Add(headerBorder);

            // ── Body content ────────────────────────────────────────────
            var body = new StackPanel { Margin = new Thickness(24, 20, 24, 8) };

            if (isError)
            {
                body.Children.Add(StatusRow("\uEA39", "Failed to start", Brushes.Tomato));
                body.Children.Add(InfoText(errorMsg ?? "Unknown error", true));
            }
            else
            {
                body.Children.Add(StatusRow("\uE73E", "Connected & Ready", new SolidColorBrush(Color.FromRgb(72, 199, 142))));

                body.Children.Add(Separator());
                body.Children.Add(InfoRow("Port", port.ToString()));
                body.Children.Add(InfoRow("Endpoint", $"http://localhost:{port}/"));
                body.Children.Add(InfoRow("API routes (MCP tools)", $"{toolCount} registered"));
                body.Children.Add(InfoRow("Version", App.Version));

                body.Children.Add(Separator());
                body.Children.Add(new TextBlock
                {
                    Text = "Cursor MCP should list the same tool count (reload MCP if the IDE shows fewer).\nThis dialog will auto-close in 8 seconds.",
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 160)),
                    FontSize = 11.5,
                    Margin = new Thickness(0, 4, 0, 0),
                    LineHeight = 18
                });
            }

            stack.Children.Add(body);

            // ── Footer ──────────────────────────────────────────────────
            var footer = new Border
            {
                CornerRadius = new CornerRadius(0, 0, 12, 12),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 28)),
                Padding = new Thickness(24, 12, 24, 12)
            };

            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            footerGrid.Children.Add(new TextBlock
            {
                Text = "rvt-ai  \u2022  Revit 2025  \u2022  .NET 8",
                Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 110)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });

            var okBtn = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(56, 132, 232),
                    Color.FromRgb(104, 72, 232), 90),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(20, 6, 20, 6),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            okBtn.Child = new TextBlock
            {
                Text = isError ? "Close" : "OK",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            okBtn.MouseLeftButtonUp += (s, e) => Close();
            Grid.SetColumn(okBtn, 1);
            footerGrid.Children.Add(okBtn);

            footer.Child = footerGrid;
            stack.Children.Add(footer);

            return stack;
        }

        private static UIElement StatusRow(string icon, string text, Brush color)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            sp.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = color,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            sp.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = color,
                VerticalAlignment = VerticalAlignment.Center
            });

            return sp;
        }

        private static UIElement InfoRow(string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 145)),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            var val = new TextBlock
            {
                Text = value,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 235)),
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(val, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(val);
            return grid;
        }

        private static UIElement InfoText(string text, bool isError)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = isError ? Brushes.Tomato : new SolidColorBrush(Color.FromRgb(180, 180, 200)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 4),
                LineHeight = 18
            };
        }

        private static UIElement Separator()
        {
            return new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Color.FromRgb(45, 45, 60)),
                Margin = new Thickness(0, 10, 0, 10)
            };
        }
    }
}
