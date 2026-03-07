using Avalonia.Controls;
using ScottPlot;
using ScottPlot.Avalonia;
using UART.ViewModels;

namespace UART.Views;

public partial class GraphView : UserControl
{
    private GraphViewModel? _viewModel;

    public GraphView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PlotDataUpdated -= UpdatePlot;

        _viewModel = DataContext as GraphViewModel;

        if (_viewModel != null)
            _viewModel.PlotDataUpdated += UpdatePlot;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitPlot();
    }

    private AvaPlot? GetAvaPlot() => this.FindControl<AvaPlot>("AvaPlot");

    private void InitPlot()
    {
        var avaPlot = GetAvaPlot();
        if (avaPlot == null) return;

        ApplyDarkTheme(avaPlot.Plot);
        avaPlot.Refresh();
    }

    private static void ApplyDarkTheme(Plot plot)
    {
        plot.FigureBackground.Color = Color.FromHex("#11111B");
        plot.DataBackground.Color = Color.FromHex("#181825");
        plot.Axes.Color(Color.FromHex("#BAC2DE"));
        plot.Grid.MajorLineColor = Color.FromHex("#313244");
    }

    private void UpdatePlot()
    {
        var avaPlot = GetAvaPlot();
        if (avaPlot == null || _viewModel == null || !IsVisible) return;

        var ys = _viewModel.GetYData();

        avaPlot.Plot.Clear();
        ApplyDarkTheme(avaPlot.Plot);

        if (ys.Length > 0)
        {
            var sig = avaPlot.Plot.Add.Signal(ys);
            sig.Color = Color.FromHex("#89B4FA");
        }

        avaPlot.Plot.Axes.AutoScale();
        avaPlot.Refresh();
    }
}
