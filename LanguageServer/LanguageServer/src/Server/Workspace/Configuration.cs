using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YarnLanguageServer
{
    internal class Configuration
    {
        // This whole setup will probably get reworked once I get a days away from staring at Visual Studio configuaration documentation
        private bool csharplookup = false;
        private bool deepCommandLookup = true;

        private bool needReinitialize = false;

        public bool CSharpLookup
        {
            get => csharplookup;
            set
            {
                if (csharplookup != value)
                {
                    csharplookup = value;
                    needReinitialize = true;
                }
            }
        }

        public bool DeepCommandLookup
        {
            get => deepCommandLookup;
            set
            {
                if (deepCommandLookup != value)
                {
                    deepCommandLookup = value;
                    needReinitialize = workspace.UnmatchedDefinitions.Any();
                }
            }
        }

        public float DidYouMeanThreshold { get; set; } = 0.24f;
        public bool OnlySuggestDeclaredVariables { get; set; } = true;

        private Workspace workspace;

        public Configuration(Workspace workspace)
        {
            this.workspace = workspace;
        }

        public void Initialize(JArray values)
        {
            if (values == null) { return; }
            try
            {
                // todo: populating itself, not the cleanest way to do this
                JsonSerializer.CreateDefault().Populate(values[0].CreateReader(), this);
            }
            catch (Exception) { }
            needReinitialize = false;
        }

        public void Update(JToken wrappedValue)
        {
            if (wrappedValue == null) { return; }
            try
            {
                // todo clean up this late night code
                var value = wrappedValue.Children().FirstOrDefault().Children().FirstOrDefault();
                JsonSerializer.CreateDefault().Populate(value.CreateReader(), this);
            }
            catch (Exception) { }
            if (needReinitialize)
            {
                needReinitialize = false;
                workspace.LoadExternalInfo();
            }
        }
    }
}