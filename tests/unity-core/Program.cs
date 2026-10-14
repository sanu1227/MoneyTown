using var catalog = typeof(MoneyTown.CoreChecks).Assembly.GetManifestResourceStream("information-catalog");
using var reader = new System.IO.StreamReader(catalog);
using var previous = typeof(MoneyTown.CoreChecks).Assembly.GetManifestResourceStream("information-catalog-v1");
using var previousReader = new System.IO.StreamReader(previous);
using var second = typeof(MoneyTown.CoreChecks).Assembly.GetManifestResourceStream("information-catalog-v2");
using var secondReader = new System.IO.StreamReader(second);
MoneyTown.InformationCatalog.Load(reader.ReadToEnd(),previousReader.ReadToEnd(),secondReader.ReadToEnd());
System.Console.WriteLine($"MoneyTown Unity C# core: {MoneyTown.CoreChecks.Run()} checks passed.");
