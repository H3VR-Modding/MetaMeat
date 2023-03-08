using System.Data;
using System.Diagnostics.CodeAnalysis;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace MetaMeat;

public class H3DataSet
{
    private readonly AssetsManager _am;
    private readonly AssetsFileInstance _instance;

    public DataSet Data { get; }
    public ObjectTableDefTools ObjectTableDef { get; private set; } = null!;
    
    public H3DataSet(string gameBasePath)
    {
        string gameResourcesPath = Path.Combine(gameBasePath, @"h3vr_Data\resources.assets");
        string gameManagedPath = Path.Combine(gameBasePath, @"h3vr_Data\Managed\");
        _am = new AssetsManager();
        _am.LoadClassPackage("res/classdata.tpk");
        _am.SetMonoTempGenerator(new MonoCecilTempGenerator(gameManagedPath));
        _instance = _am.LoadAssetsFile(gameResourcesPath, true);
        _am.LoadClassDatabaseFromPackage(_instance.file.Metadata.UnityVersion);
        Data = new DataSet();
    }

    // Adds all allowed data from the asset bundle into the dataset
    public void PopulateData()
    {
        // Find all MonoBehaviour Assets in the resources file
        foreach (var inf in _instance.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        {
            // Skip over the FMOD stuff because that crashes the AssetsTools.NET library
            var baseField = _am.GetBaseField(_instance, inf);
            AddDataPoint(baseField);
        }

        // Setup the primary keys for each table
        SetPrimaryKeys();
        InitializeToolClasses();
        
        // Dispose of assets now we're done with them
        _am.UnloadAll();
    }

    // Given an asset from the asset bundle, add it to the dataset. Creates tables and columns as required.
    private void AddDataPoint(AssetTypeValueField asset)
    {
        // Get the script name from the asset. If it's not one we care about, ignore it.
        string scriptName = _am.GetExtAsset(_instance, asset["m_Script"]).baseField["m_Name"].AsString;
        if (!IncludeTypeNames.Contains(scriptName)) return;

        // Create a row for this data point
        DataTable table = Data.Tables.Contains(scriptName) ? Data.Tables[scriptName]! : Data.Tables.Add(scriptName);
        bool creating = table.Columns.Count == 0;
        DataRow row = table.NewRow();

        // Iterate over all the fields and make/add the columns required
        foreach (AssetTypeValueField field in asset.Children)
        {
            // Ignore all fields starting with m_ because we likely don't care about those
            string fieldName = field.FieldName;
            if (IgnoredFieldNames.Contains(fieldName)) continue;

            // Check if we know how to deal with this type
            AssetTypeTemplateField fieldTemplate = field.TemplateField;
            if (TryGetGetMappingDefinition(field.TemplateField, out AssetFieldMapping? fieldMapping))
            {
                // If we don't want to include this just continue
                if (!fieldMapping.IncludeInDataSet) continue;

                // Yep. Make a column for it if needed and then add it to the row
                if (creating) table.Columns.Add(fieldName, fieldMapping.SystemType);
                row[fieldName] = fieldMapping.GetValue(this, field);
            }
            else if (field.TemplateField.ValueType == AssetValueType.Array)
            {
                // If it's an array we can do the same check but just need to map it to an array instead
                AssetTypeTemplateField arrayTemplate = field.TemplateField.Children[1];
                if (!TryGetGetMappingDefinition(arrayTemplate, out fieldMapping))
                    throw new Exception($"I don't know how to deal with field of type {arrayTemplate.ValueType} / {arrayTemplate.Type}");

                // Same thing as above
                if (!fieldMapping.IncludeInDataSet) continue;
                if (creating) table.Columns.Add(fieldName, fieldMapping.SystemType.MakeArrayType());
                row[fieldName] = fieldMapping.GetValueArray(this, field.Children);
            }
            else throw new Exception($"I don't know how to deal with field of type {fieldTemplate.ValueType} / {fieldTemplate.Type}");
        }

        // When we're done with all the columns add the row to the table
        table.Rows.Add(row);
    }

    // Assigns the primary keys to each table to make lookups possible
    private void SetPrimaryKeys()
    {
        var fvrObjects = Data.Tables["FVRObject"];
        fvrObjects!.PrimaryKey = new[] {fvrObjects.Columns["ItemID"]!};

        var itemSpawnerIds = Data.Tables["ItemSpawnerID"];
        itemSpawnerIds!.PrimaryKey = new[] {itemSpawnerIds.Columns["ItemID"]!};
        
        var objectTableDef = Data.Tables["ObjectTableDef"];
        objectTableDef!.PrimaryKey = new[] {objectTableDef.Columns["m_Name"]!};
    }

    private void InitializeToolClasses()
    {
        ObjectTableDef = new ObjectTableDefTools(Data.Tables["ObjectTableDef"]!);
    }
    
    // Returns the field mapping that corresponds to the given field template
    private static bool TryGetGetMappingDefinition(AssetTypeTemplateField fieldTemplate, [MaybeNullWhen(false)] out AssetFieldMapping assetFieldMapping)
    {
        return LookupValueType.TryGetValue(fieldTemplate.ValueType, out assetFieldMapping)
               || LookupTypeName.TryGetValue(fieldTemplate.Type, out assetFieldMapping);
    }

    // Given an asset field of type PPtr<$FVRObject> or PPtr<$ItemSpawnerID>, returns the corresponding object's ItemID
    // No idea why they both use ItemID for this field but it makes it convenient to reuse the same method so :)
    private string? MapFvrObjectPPtr(AssetTypeValueField field)
    {
        AssetExternal extAsset = _am.GetExtAsset(_instance, field);
        if (extAsset.info is null) return null;
        return extAsset.baseField["ItemID"].AsString;
    }

    // Mappings for primitive type fields
    private static readonly Dictionary<AssetValueType, AssetFieldMapping> LookupValueType = new()
    {
        [AssetValueType.String] = new TypedAssetFieldMapping<string>(static (_, field) => field.AsString),
        [AssetValueType.Int32] = new TypedAssetFieldMapping<int>(static (_, field) => field.AsInt),
        [AssetValueType.Float] = new TypedAssetFieldMapping<float>(static (_, field) => field.AsFloat),
        [AssetValueType.Bool] = new TypedAssetFieldMapping<bool>(static (_, field) => field.AsBool),
    };

    // Mappings for complex type fields (inline serialized data, PPtrs, etc)
    private static readonly Dictionary<string, AssetFieldMapping> LookupTypeName = new()
    {
        ["PPtr<$FVRObject>"] = new TypedAssetFieldMapping<string?>(static (@this, field) => @this.MapFvrObjectPPtr(field)),
        ["PPtr<$ItemSpawnerID>"] = new TypedAssetFieldMapping<string?>(static (@this, field) => @this.MapFvrObjectPPtr(field)),
        ["PPtr<$Sprite>"] = new NoAssetFieldMapping(),
        ["PPtr<$ItemSpawnerControlInfographic>"] = new NoAssetFieldMapping(),
    };

    private static readonly HashSet<string> IgnoredFieldNames = new() {"m_GameObject", "m_Enabled", "m_Script", "m_anvilPrefab"};
    private static readonly HashSet<string> IncludeTypeNames = new() { "FVRObject", "ItemSpawnerID", "ObjectTableDef" };

    // Represents a mapping between a field in the asset bundle and a field in a row of the DataSet
    private abstract class AssetFieldMapping
    {
        protected Type? SystemTypeInternal;
        public Type SystemType => SystemTypeInternal ?? throw new NullReferenceException();

        public bool IncludeInDataSet { get; protected init; } = true;

        public abstract object GetValue(H3DataSet dsc, AssetTypeValueField field);

        public abstract object GetValueArray(H3DataSet dsc, IEnumerable<AssetTypeValueField> fields);
    }

    // Generic implementation of AssetFieldMapping
    // Required so the return value of the GetValue and GetValueArray methods are correct
    private class TypedAssetFieldMapping<TRet> : AssetFieldMapping
    {
        public delegate TRet MapFieldValueToObject(H3DataSet @this, AssetTypeValueField field);

        private readonly MapFieldValueToObject _mapFunc;

        public TypedAssetFieldMapping(MapFieldValueToObject mapFunc)
        {
            SystemTypeInternal = typeof(TRet);
            _mapFunc = mapFunc;
        }

        public override object GetValue(H3DataSet dsc, AssetTypeValueField field) => _mapFunc(dsc, field)!;
        public override object GetValueArray(H3DataSet dsc, IEnumerable<AssetTypeValueField> fields) => fields.Select(f => _mapFunc(dsc, f)).ToArray();
    }

    // For data fields we don't care about
    private class NoAssetFieldMapping : AssetFieldMapping
    {
        public NoAssetFieldMapping()
        {
            IncludeInDataSet = false;
        }

        public override object GetValue(H3DataSet dsc, AssetTypeValueField field) => throw new NotImplementedException();
        public override object GetValueArray(H3DataSet dsc, IEnumerable<AssetTypeValueField> fields) => throw new NotImplementedException();
    }
}
