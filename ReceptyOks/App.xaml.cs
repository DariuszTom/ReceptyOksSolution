using Microsoft.Extensions.DependencyInjection;

namespace ReceptyOks;

public partial class App : Application
{

   public App()
   {
       InitializeComponent();
       MainPage = new AppShell();
   }
}