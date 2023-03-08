using System.Data;
using MetaMeat;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

// Locate game install and read assets
string gameBasePath = SteamAppLocator.LocateGame()!;
H3DataSet dsc = new H3DataSet(gameBasePath);
dsc.PopulateData();

List<DataRow> spawnsInNoTables = new List<DataRow>();
foreach (DataRow fvrObject in dsc.Data.Tables["FVRObject"]!.Select("Category = 1 AND OSple = true"))
{
    bool spawnable = false;
    foreach (DataRow tableDef in dsc.Data.Tables["ObjectTableDef"]!.Rows)
    {
        if ((string) tableDef["m_Name"] == "FA_ALL") continue;
        
        if (ObjectTableDefTools.IsFvrObjectSpawnableIn(tableDef, fvrObject))
        {
            spawnable = true;
            break;
        }
    }
    
    if (!spawnable) spawnsInNoTables.Add(fvrObject);
}

Console.WriteLine($"Count of objects not spawnable in TNH: {spawnsInNoTables.Count}");
foreach (var spawnable in spawnsInNoTables)
{
    Console.WriteLine($"  - {spawnable["ItemID"]}");
}
