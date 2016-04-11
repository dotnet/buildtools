// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class GenerationsTests
    {
        private Generations _generations;
        private Log _log;


        public GenerationsTests(ITestOutputHelper output)
        {
            _generations = Generations.Load("generations.json", false);
            _log = new Log(output);
        }

        [Fact]
        public void Generations_MaxVersion()
        {
            _log.Reset();
            var generation = _generations.DetermineGenerationFromSeeds("System.Runtime", new Version(4, 0, 30, 0), _log);
            Assert.Equal(new Version(1, 3, 0, 0), generation);
            _log.AssertNoErrorsOrWarnings();
        }

        [Fact]
        public void Generations_MidVersion()
        {
            _log.Reset();
            var generation = _generations.DetermineGenerationFromSeeds("System.Runtime", new Version(4, 0, 15, 0), _log);
            Assert.Equal(new Version(1, 2, 0, 0), generation);
            _log.AssertNoErrorsOrWarnings();
        }

        [Fact]
        public void Generations_NotTracked()
        {
            _log.Reset();
            var generation = _generations.DetermineGenerationFromSeeds("System.Banana", new Version(4, 0, 0, 0), _log);
            Assert.Equal(null, generation);
            _log.AssertNoErrorsOrWarnings();
        }

        [Fact]
        public void Generations_PreVersion()
        {
            _log.Reset();
            var generation = _generations.DetermineGenerationFromSeeds("System.Runtime", new Version(3, 0, 0, 0), _log);
            Assert.Equal(null, generation);
            // expect an error since the contract is tracked with generations but this version is lower than any mapping
            Assert.Equal(1, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
        }

        [Fact]
        public void Generations_MatchesInbox()
        {
            FrameworkSet fxs = FrameworkSet.Load("FrameworkLists");
            Version maxVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

            foreach (var fxGroup in fxs.Frameworks)
            {
                foreach (var fx in fxGroup.Value)
                {
                    var thisFx = new NuGetFramework(fx.FrameworkName.Identifier, fx.FrameworkName.Version, fx.FrameworkName.Profile);
                    var fxGeneration = Generations.DetermineGenerationForFramework(thisFx, false);

                    foreach (var assembly in fx.Assemblies.Where(a => !s_classicAssemblies.Contains(a.Key) && a.Value != maxVersion))
                    {
                        _log.Reset();
                        Version assmGeneration = _generations.DetermineGenerationFromSeeds(assembly.Key, assembly.Value, _log);

                        Version effectiveFxGeneration;
                        if (!s_generationException.TryGetValue(Tuple.Create(fx.FrameworkName, assembly.Key), out effectiveFxGeneration))
                        {
                            effectiveFxGeneration = fxGeneration;
                        }

                        Assert.Equal(0, _log.ErrorsLogged);
                        Assert.Equal(0, _log.WarningsLogged);
                        Assert.True(null != assmGeneration, $"{assembly.Key},{assembly.Value} should be tracked by generations");
                        Assert.True(assmGeneration.Major >= 1 && assmGeneration.Minor >= 0);
                        Assert.True(assmGeneration <= effectiveFxGeneration, $"Generation {assmGeneration} of {assembly.Key}, {assembly.Value} must be less than or equal to {fxGeneration} since this assembly is inbox in {fx.FrameworkName} which is mapped to generation {effectiveFxGeneration}.");
                    }
                }
            }
        }       

        private static readonly FrameworkName s_net46 = new FrameworkName(".NETFramework,Version=v4.6");
        private static readonly Version s_v14 = new Version(1, 4, 0, 0);

        private static Dictionary<Tuple<FrameworkName, string>, Version> s_generationException = new Dictionary<Tuple<FrameworkName, string>, Version>()
        {
            // NetworkInformation 4.0.10 was supported in 4.6, but not yet on UWP, for now we are restricting 4.0.10 to netstandard1.4
            // (hiding the new surface area from PCLs targeting net46) until 
            { Tuple.Create(s_net46, "System.Net.NetworkInformation"), s_v14 }
        };

        private static HashSet<string> s_classicAssemblies = new HashSet<string>()
        {
            "Accessibility",
            "CustomMarshalers",
            "ISymWrapper",
            "Microsoft.Activities.Build",
            "Microsoft.Build.Conversion.v4.0",
            "Microsoft.Build",
            "Microsoft.Build.Engine",
            "Microsoft.Build.Framework",
            "Microsoft.Build.Tasks.v4.0",
            "Microsoft.Build.Utilities.v4.0",
            "Microsoft.CSharp",
            "Microsoft.JScript",
            "Microsoft.VisualBasic.Compatibility.Data",
            "Microsoft.VisualBasic.Compatibility",
            "Microsoft.VisualBasic",
            "Microsoft.VisualC",
            "Microsoft.VisualC.STLCLR",
            "mscorlib",
            "mscorlib.Extensions",
            "PresentationBuildTasks",
            "PresentationCore",
            "PresentationFramework.Aero",
            "PresentationFramework.Aero2",
            "PresentationFramework.AeroLite",
            "PresentationFramework.Classic",
            "PresentationFramework",
            "PresentationFramework.Luna",
            "PresentationFramework.Royale",
            "ReachFramework",
            "sysglobl",
            "System.Activities.Core.Presentation",
            "System.Activities",
            "System.Activities.DurableInstancing",
            "System.Activities.Presentation",
            "System.Activities.Statements",
            "System.AddIn.Contract",
            "System.AddIn",
            "System.ComponentModel.Composition",
            "System.ComponentModel.Composition.Registration",
            "System.ComponentModel.DataAnnotations",
            "System.Configuration",
            "System.Configuration.Install",
            "System.Core",
            "System.Data.DataSetExtensions",
            "System.Data",
            "System.Data.Entity.Design",
            "System.Data.Entity",
            "System.Data.Linq",
            "System.Data.OracleClient",
            "System.Data.Services.Client",
            "System.Data.Services.Design",
            "System.Data.Services",
            "System.Data.SqlXml",
            "System.Deployment",
            "System.Design",
            "System.Device",
            "System.DirectoryServices.AccountManagement",
            "System.DirectoryServices",
            "System.DirectoryServices.Protocols",
            "System",
            "System.Drawing.Design",
            "System.Drawing",
            "System.Dynamic",
            "System.EnterpriseServices",
            "System.EnterpriseServices.Thunk",
            "System.EnterpriseServices.Wrapper",
            "System.IdentityModel",
            "System.IdentityModel.Selectors",
            "System.IdentityModel.Services",
            "System.IO.Log",
            "System.IO.Compression.FileSystem",
            "System.Management",
            "System.Management.Instrumentation",
            "System.Messaging",
            "System.Net",
            "System.Net.Http.WebRequest",
            "System.Numerics",
            "System.Observable",
            "System.Printing",
            "System.Runtime.Caching",
            "System.Runtime.DurableInstancing",
            "System.Runtime.Remoting",
            "System.Runtime.Serialization",
            "System.Runtime.Serialization.Formatters.Soap",
            "System.Security",
            "System.ServiceModel.Activation",
            "System.ServiceModel.Activities",
            "System.ServiceModel.Channels",
            "System.ServiceModel.Discovery",
            "System.ServiceModel",
            "System.ServiceModel.Routing",
            "System.ServiceModel.Web",
            "System.ServiceProcess",
            "System.Speech",
            "System.Transactions",
            "System.Web.Abstractions",
            "System.Web.ApplicationServices",
            "System.Web.DataVisualization.Design",
            "System.Web.DataVisualization",
            "System.Web",
            "System.Web.DynamicData.Design",
            "System.Web.DynamicData",
            "System.Web.Entity.Design",
            "System.Web.Entity",
            "System.Web.Extensions.Design",
            "System.Web.Extensions",
            "System.Web.Mobile",
            "System.Web.RegularExpressions",
            "System.Web.Routing",
            "System.Web.Services",
            "System.Windows",
            "System.Windows.Controls.Ribbon",
            "System.Windows.Forms.DataVisualization.Design",
            "System.Windows.Forms.DataVisualization",
            "System.Windows.Forms",
            "System.Windows.Input.Manipulations",
            "System.Windows.Presentation",
            "System.Workflow.Activities",
            "System.Workflow.ComponentModel",
            "System.Workflow.Runtime",
            "System.WorkflowServices",
            "System.Xaml",
            "System.Xml",
            "System.Xml.Linq",
            "System.Xml.Serialization",
            "UIAutomationClient",
            "UIAutomationClientsideProviders",
            "UIAutomationProvider",
            "UIAutomationTypes",
            "WindowsBase",
            "WindowsFormsIntegration",
            "XamlBuildTask",
            "Microsoft.Devices.Sensors",
            "Microsoft.Phone",
            "Microsoft.Phone.Interop",
            "Microsoft.Phone.Reactive",
            "Microsoft.Phone.Controls",
            "Microsoft.Phone.Controls.Maps",
            "Microsoft.Phone.Maps",
            "Microsoft.Xna.Framework",
            "Microsoft.Xna.Framework.Game",
            "Microsoft.Xna.Framework.GamerServices",
            "Microsoft.Xna.Framework.Graphics",
            "Microsoft.Xna.Framework.Input.Touch",
            "Microsoft.Xna.Framework.Avatar",
            "Microsoft.Xna.Framework.GamerServicesExtensions",
            "Microsoft.Xna.Framework.MediaLibraryExtensions",
            "Microsoft.Xna.Framework.Interop"
        };
    }
}
