using System.Data;

namespace MetaMeat;

public class ObjectTableDefTools : BaseTools
{
    public ObjectTableDefTools(DataTable dataTable) : base(dataTable)
    {
    }

    public List<DataRow> GetSpawnableObjectsFrom(DataRow objectTableDef)
    {
        return ObjectTable.DataSet!.Tables["FVRObject"]!.Rows.Cast<DataRow>().Where(fvrObject => IsFvrObjectSpawnableIn(objectTableDef, fvrObject)).ToList();
    }

    public List<DataRow> GetTablesObjectSpawnsIn(DataRow fvrObject)

    {
        return ObjectTable.Rows.Cast<DataRow>().Where(objectTableDef => IsFvrObjectSpawnableIn(objectTableDef, fvrObject)).ToList();
    }
    
    public static bool IsFvrObjectSpawnableIn(DataRow objectTableDef, DataRow fvrObject)
    {
        bool CheckTag(string tableFieldName, string objectFieldName)
        {
            var allowedTableTags = (int[]) objectTableDef[tableFieldName];
            var objectTag = (int) fvrObject[objectFieldName];
            return allowedTableTags.Length == 0 || allowedTableTags.Contains(objectTag);
        }

        bool CheckTagArray(string tableFieldName, string objectFieldName)
        {
            var allowedTableTags = (int[]) objectTableDef[tableFieldName];
            var objectTags = (int[]) fvrObject[objectFieldName];
            return allowedTableTags.Length == 0 || allowedTableTags.Any(i => objectTags.Contains(i));
        }
        
        bool CheckTagArrayInverted(string tableFieldName, string objectFieldName)
        {
            var allowedTableTags = (int[]) objectTableDef[tableFieldName];
            var objectTags = (int[]) fvrObject[objectFieldName];
            return allowedTableTags.Length == 0 || !allowedTableTags.Any(i => objectTags.Contains(i));
        }
        
        if ((bool) objectTableDef["UseIDListOverride"])
        {
            // This table uses only a specific list of items.
            var allowedIds = (string[]) objectTableDef["IDOverride"];
            var objectId = (string) fvrObject["ItemID"];
            return allowedIds.Contains(objectId);
        }
        
        // Match category
        int tableCat = (int) objectTableDef["Category"], objectCat = (int) fvrObject["Category"];
        if (tableCat != objectCat) return false;
        
        // OSple must be enabled for the object
        if (!(bool) fvrObject["OSple"]) return false;
        
        // Min and max capacity
        int tableCapMin = (int) objectTableDef["MinAmmoCapacity"], objectCapMin = (int) fvrObject["MinCapacityRelated"];
        int tableCapMax = (int) objectTableDef["MaxAmmoCapacity"], objectCapMax = (int) fvrObject["MaxCapacityRelated"];
        if (tableCapMin > -1 && objectCapMax < tableCapMin) return false;
        if (tableCapMax > -1 && objectCapMin > tableCapMax) return false;
        
        // Most of the tags
        if (!CheckTag("Eras", "TagEra")) return false;
        if (!CheckTag("Sets", "TagSet")) return false;
        if (!CheckTag("Sizes", "TagFirearmSize")) return false;
        if (!CheckTag("Actions", "TagFirearmAction")) return false;
        if (!CheckTag("RoundPowers", "TagFirearmRoundPower")) return false;
        if (!CheckTag("PowerupTypes", "TagPowerupType")) return false;
        if (!CheckTag("ThrownTypes", "TagThrownType")) return false;
        if (!CheckTag("ThrownDamageTypes", "TagThrownDamageType")) return false;
        if (!CheckTag("MeleeStyles", "TagMeleeStyle")) return false;
        if (!CheckTag("MeleeHandedness", "TagMeleeHandedness")) return false;
        if (!CheckTag("MountTypes", "TagAttachmentMount")) return false;
        if (!CheckTag("Features", "TagAttachmentFeature")) return false;

        // These are both arrays
        if (!CheckTagArray("Modes", "TagFirearmFiringModes")) return false;
        if (!CheckTagArray("Feedoptions", "TagFirearmFeedOption")) return false;
        if (!CheckTagArray("MountsAvailable", "TagFirearmMounts")) return false;
        
        // This one is inverted
        if (!CheckTagArrayInverted("ExcludeModes", "TagFirearmFiringModes")) return false;
        
        // If everything passed then I guess we're good
        return true;
    }
}
