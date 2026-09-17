namespace App.Mobile;

public partial class GaiaLifeApp : Application
{
    public GaiaLifeApp()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "Gaia-Life" };
    }
}
