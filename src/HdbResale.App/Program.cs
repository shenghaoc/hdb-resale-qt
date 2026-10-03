using Qt.Quick;
namespace HdbResale.App;
internal static class Program
{
    private static void Main(string[] args)
    {
        Qml.LoadFromRootModule("Main");
        Qml.WaitForExit();
    }
}
