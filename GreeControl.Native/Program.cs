namespace GreeControl.Native;

static class Program
{
    [STAThread]
    static void Main()
    {
        var app = new System.Windows.Application();
        app.Run(new Form1());
    }
}
