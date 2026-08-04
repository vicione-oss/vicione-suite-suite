using System.Reflection;
using Core.UiHosting;
using TestModule.Client;

namespace Blazor.Server.Tests.Helpers
{
    internal class BlazorServerHelpers
    {
        private static readonly Assembly _blazorServerTestAssembly = typeof(TestClientModule).Assembly;

        public static IEnumerable<IUiModuleBundle> GetUiModuleBundlesWithUiModules()
        {
            var uiBundleTestClientModule = Substitute.For<IUiModuleBundle>();
            uiBundleTestClientModule.Module.Returns(new TestClientModule());
            uiBundleTestClientModule.AssemblyLocation.Returns(_blazorServerTestAssembly.Location);
            uiBundleTestClientModule.Assembly.Returns(_blazorServerTestAssembly);

            var uiBundles = new List<IUiModuleBundle>
            {
                uiBundleTestClientModule,
            };
            return uiBundles;
        }

        public static IEnumerable<IUiModuleBundle> GetUiModuleBundlesWithBlazorServerUiModules()
        {
            var uiBundleTestClientModule = Substitute.For<IUiModuleBundle>();
            uiBundleTestClientModule.Module.Returns(new TestBlazorServerClientModule());
            uiBundleTestClientModule.AssemblyLocation.Returns(_blazorServerTestAssembly.Location);
            uiBundleTestClientModule.Assembly.Returns(_blazorServerTestAssembly);

            var uiBundles = new List<IUiModuleBundle>
            {
                uiBundleTestClientModule,
            };
            return uiBundles;
        }

    }
}
