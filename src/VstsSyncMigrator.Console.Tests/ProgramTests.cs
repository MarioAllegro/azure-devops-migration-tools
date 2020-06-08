﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using VstsSyncMigrator.Engine.Configuration;
using VstsSyncMigrator.Engine.Configuration.FieldMap;
using System.IO;

namespace VstsSyncMigrator.Console.Tests
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void TestSeraliseToJson()
        {
            var json = JsonConvert.SerializeObject(EngineConfiguration.GetDefault(),
                    new FieldMapConfigJsonConverter(),
                    new ProcessorConfigJsonConverter());
            var sw = new StreamWriter("configuration.json");
            sw.WriteLine(json);
            sw.Close();

        }

        [TestMethod]
        public void TestDeseraliseFromJson()
        {
            EngineConfiguration ec;
            var sr = new StreamReader("configuration.json");
            var configurationjson = sr.ReadToEnd();
            sr.Close();
            ec = JsonConvert.DeserializeObject<EngineConfiguration>(configurationjson,
                new FieldMapConfigJsonConverter(),
                new ProcessorConfigJsonConverter());

        }
    }
}
