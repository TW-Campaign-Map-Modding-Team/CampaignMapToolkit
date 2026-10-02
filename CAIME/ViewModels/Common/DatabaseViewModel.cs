using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Xml.Linq;
using CAIME.Models;

namespace CAIME
{
    public class DatabaseViewModel : BaseViewModel
    {
        private string  _asskitPath;
        private string _campaignMapName;
        private IReadOnlyDictionary<string, XDocument> _preparedTables;

        public DataSet DataSet { get; private set; }

        private DataTable table_campaign_map_regions;
        private DataTable table_regions;
        private DataTable table_ground_types;
        private DataTable table_climates;
        private DataTable table_attritions;
        private DataTable table_campaign_map_roads;
        private DataTable table_campaigns;
        private DataTable table_regions_to_provinces;

        public List<DBRegion>           CachedRegions;
        public List<DBGroundType>       CachedGroundTypes;
        public List<DBAttrition>        CachedAttritions;
        public List<DBClimate>          CachedClimates;
        public List<DBAreaOfInterest>   CachedAreasOfInterest;
        public List<DBCampaign>         CachedCampaigns;
        public List<DBRoad>             CachedRoads;
        public List<DBRegionToProvince> CachedRegionsToProvinces;

        public DatabaseViewModel()
        {
            DataSet                     = new DataSet();
            CachedRegions               = new List<DBRegion>();
            CachedGroundTypes           = new List<DBGroundType>();
            CachedAttritions            = new List<DBAttrition>();
            CachedClimates              = new List<DBClimate>();
            CachedAreasOfInterest       = new List<DBAreaOfInterest>();
            CachedRoads                 = new List<DBRoad>();
            CachedCampaigns             = new List<DBCampaign>();
            CachedRegionsToProvinces    = new List<DBRegionToProvince>();
        }

        /// <param name="preparedTables">
        /// Table data to load in place of the Assembly Kit's own files, as Assembly Kit data XML keyed
        /// by table name - the RPFM database source's. The Assembly Kit still supplies every table's
        /// schema.
        /// </param>
        public bool Initialise(GameTemplate game, string projectPath, MapHexFile mapHexFile, IReadOnlyDictionary<string, XDocument> preparedTables = null)
        {
            ShutDown();

            this._asskitPath        = PreferencesViewModel.Instance.GetAssKitPath(game);
            this._campaignMapName   = mapHexFile.CampaignMapName;
            this._preparedTables    = preparedTables;

            LoadTables(game);

            DataSet.AcceptChanges();

            CacheData(game);

            if (!VerifyRegionIndices(game, mapHexFile))
            {
                return false;
            }

            return true;
        }

        private void ResetDatabase()
        {
            CachedAreasOfInterest.Clear();
            CachedAttritions.Clear();
            CachedCampaigns.Clear();
            CachedClimates.Clear();
            CachedGroundTypes.Clear();
            CachedRegions.Clear();
            CachedRegionsToProvinces.Clear();
            CachedRoads.Clear();
            DataSet.Reset();
        }

        public void ShutDown()
        {
            ResetDatabase();
        }

        public bool Save(GameTemplate game, MapHexFile mapHexFile)
        {
            //UpdateRegionIndices(game, mapHexFile);
            return true;
        }

        public DataTable LoadTable(string tableName)
        {
            var table       = new DelayedDataTable(tableName);
            var schema_path = $@"{_asskitPath}\raw_data\db\TWaD_{tableName}.xml";
            var table_path  = $@"{_asskitPath}\raw_data\db\{tableName}.xml";

            var schema_doc  = XDocument.Load(schema_path);
            var table_doc   = _preparedTables != null && _preparedTables.TryGetValue(tableName, out var prepared)
                ? prepared
                : XDocument.Load(table_path);

            var schema = new XmlSchema();
            schema.Load(schema_doc);

            var primaryKey = new List<DataColumn>();
                
            foreach (var field in schema.Fields)
            {
                var column = new DataColumn
                {
                    ColumnName  = field.Name,
                    DataType    = XmlSchema.GetFieldType(field),
                };

                if (field.DefaultValue != null)
                {
                    column.DefaultValue = field.DefaultValue;
                }

                table.Columns.Add(column);
                if (field.PrimaryKey == 1)
                {
                    primaryKey.Add(column);
                }
            }

            table.PrimaryKey = primaryKey.ToArray();

            var recordNodes = table_doc.Root.Elements(tableName);
            foreach (var recordNode in recordNodes)
            {
                var record = table.NewRow() as PendingDataRow;
                for (int j = 0; j < schema.GetFieldsCount(); ++j)
                {
                    var fieldName   = schema.GetField(j).Name;
                    var field       = recordNode.Element(fieldName);
                    var fieldValue  = field == null ? schema.GetField(j).DefaultValue.ToString() : recordNode.Element(fieldName).Value;

                    if (schema.GetField(j).Type == XmlSchemaFieldType.YesNo)
                    {
                        fieldValue = fieldValue == "1" ? "true" : "false";
                    }
                    else
                    if (schema.GetField(j).Type == XmlSchemaFieldType.Double)
                    {
                        // Round-tripped through the invariant culture: the typed DataColumn parses
                        // the string back with InvariantCulture, so a comma-decimal locale would
                        // otherwise write "1,5" here and fail to read it.
                        fieldValue = double.Parse(fieldValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    if (schema.GetField(j).Type == XmlSchemaFieldType.Single)
                    {
                        fieldValue = float.Parse(fieldValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                    }

                    record[fieldName] = fieldValue;
                }

                table.Rows.Add(record);
            }

            table.AcceptChanges();

            DataSet.Tables.Add(table);

            return table;
        }

        private void LoadTables(GameTemplate game)
        {
            var tables = new Dictionary<string, DataTable>();

            // The required-table set is centralised so the Assembly Kit and RPFM workflows stay in sync.
            foreach (var tableName in Rpfm.DatabaseTableProvider.GetRequiredTables(game))
            {
                tables[tableName] = LoadTable(tableName);
            }

            table_campaign_map_regions  = tables[Constants.TABLE_CAMPAIGN_MAP_REGIONS];
            table_regions               = tables[Constants.TABLE_REGIONS];
            table_ground_types          = tables[Constants.TABLE_CAMPAIGN_GROUND_TYPES];
            table_climates              = tables[Constants.TABLE_CLIMATES];
            table_attritions            = tables[Constants.TABLE_CAMPAIGN_MAP_ATTRITIONS];
            table_campaign_map_roads    = tables[Constants.TABLE_CAMPAIGN_MAP_ROADS];
            table_campaigns             = tables[Constants.TABLE_CAMPAIGNS];
            table_regions_to_provinces  = tables[Constants.TABLE_REGIONS_TO_PROVINCES];
        }

        public DataTable GetTable(string name) => DataSet.Tables[name];

        public IReadOnlyList<string> GetCombinedRegionOrder()
        {
            if (CachedRegions == null || CachedRegions.Count == 0)
                return null;

            var ordered = new List<DBRegion>(CachedRegions);
            ordered.Sort((a, b) => a.Id.CompareTo(b.Id));

            var names = new List<string>(ordered.Count);
            foreach (var region in ordered)
                names.Add(region.Key);

            return names;
        }
        public bool HasUnsavedChanges() => false;

        private void CacheRegions(GameTemplate game)
        {
            CachedRegions.Clear();

            // First cache only land regions
            foreach (DataRow dr in table_regions.Rows)
            {
                var key = dr.Field<string>("key");
                var pkey = new string[] { _campaignMapName, key };
                var campaign_map_region = table_campaign_map_regions.Rows.Find(pkey);
                if (campaign_map_region == null)
                {
                    continue;
                }

                //var map = campaign_map_region.Field<string>("campaign_map");
                //if (map != campaign_map)
                //    continue;

                var region = dr;
                var is_sea = region.Field<bool>("is_sea");
                if (is_sea == true)
                {
                    continue;
                }

                var id = CachedRegions.Count;
                var r = (byte)region.Field<int>("r");
                var g = (byte)region.Field<int>("g");
                var b = (byte)region.Field<int>("b");
                var colour = Utility.ToRgba(r, g, b);

                var dbRegion = new DBRegion()
                {
                    Id = id,
                    Key = key,
                    IsSea = is_sea,
                    Colour = colour,
                };

                if (game == GameTemplate.Pharaoh_Dynasties)
                {
                    dbRegion.Id = campaign_map_region.Field<int>("index") - 1;
                }

                CachedRegions.Add(dbRegion);
            }

            // Then cache only sea regions
            foreach (DataRow dr in table_regions.Rows)
            {
                var key = dr.Field<string>("key");
                var pkey = new string[] { _campaignMapName, key };
                var campaign_map_region = table_campaign_map_regions.Rows.Find(pkey);
                if (campaign_map_region == null)
                {
                    continue;
                }

                var region = dr;
                var is_sea = region.Field<bool>("is_sea");
                if (is_sea == false)
                {
                    continue;
                }

                var id = CachedRegions.Count;
                // var key     = region.Field<string>("key");
                var r = (byte)region.Field<int>("r");
                var g = (byte)region.Field<int>("g");
                var b = (byte)region.Field<int>("b");
                var colour = Utility.ToRgba(r, g, b);

                var dbRegion = new DBRegion()
                {
                    Id = id,
                    Key = key,
                    IsSea = is_sea,
                    Colour = colour,
                };

                if (game == GameTemplate.Pharaoh_Dynasties)
                {
                    dbRegion.Id = campaign_map_region.Field<int>("index") - 1;
                }

                CachedRegions.Add(dbRegion);
            }
        }

        private void CacheGroundTypes()
        {
            CachedGroundTypes.Clear();

            foreach (DataRow land_ground_type in table_ground_types.Rows)
            {
                var is_sea = land_ground_type.Field<bool>("is_sea");
                if (is_sea == true)
                {
                    continue;
                }

                var id = CachedGroundTypes.Count;
                var key = land_ground_type.Field<string>("type");
                var cost = land_ground_type.Field<int>("movement_cost");

                var dbGroundType = new DBGroundType()
                {
                    Id = id,
                    Key = key,
                    IsSea = is_sea,
                    MoveCost = cost,
                };

                CachedGroundTypes.Add(dbGroundType);
            }

            foreach (DataRow sea_ground_type in table_ground_types.Rows)
            {
                var is_sea = sea_ground_type.Field<bool>("is_sea");
                if (is_sea == false)
                {
                    continue;
                }

                var id = CachedGroundTypes.Count;
                var key = sea_ground_type.Field<string>("type");
                var cost = sea_ground_type.Field<int>("movement_cost");

                var dbGroundType = new DBGroundType()
                {
                    Id = id,
                    Key = key,
                    IsSea = is_sea,
                    MoveCost = cost,
                };

                CachedGroundTypes.Add(dbGroundType);
            }
        }

        private void CacheClimates()
        {
            CachedClimates.Clear();

            foreach (DataRow dr in table_climates.Rows)
            {
                var climate = new DBClimate()
                {
                    Id = CachedClimates.Count,
                    Key = dr.Field<string>("climate_type"),
                };

                CachedClimates.Add(climate);
            }
        }

        private void CacheAttritions()
        {
            CachedAttritions.Clear();

            foreach (DataRow dr in table_attritions.Rows)
            {
                var attrition_type = dr.Field<string>("type");
                if (attrition_type != "terrain_land" && attrition_type != "terrain_sea")
                {
                    continue;
                }

                var attrition = new DBAttrition()
                {
                    Id = CachedAttritions.Count,
                    Key = dr.Field<string>("key"),
                };

                CachedAttritions.Add(attrition);
            }
        }

        private void CacheAreasOfInterest(GameTemplate game)
        {
            CachedAreasOfInterest.Clear();

            if (game == GameTemplate.Three_Kingdoms ||
                game == GameTemplate.Warhammer3)
            {
                var table_areas_of_interest = GetTable(Constants.TABLE_CAMPAIGN_MAP_AREAS_OF_INTEREST);
                foreach (DataRow dr in table_areas_of_interest.Rows)
                {
                    var campaign_map_name = dr.Field<string>("campaign_map");
                    if (campaign_map_name != _campaignMapName)
                    {
                        continue;
                    }

                    var areaOfInterest = new DBAreaOfInterest()
                    {
                        Id = CachedAreasOfInterest.Count,
                        Key = dr.Field<string>("key"),
                    };

                    CachedAreasOfInterest.Add(areaOfInterest);
                }
            }
        }

        private void CacheCampaigns()
        {
            CachedCampaigns.Clear();
            
            foreach (DataRow dr in table_campaigns.Rows)
            {
                var dbCampaign = new DBCampaign()
                {
                    CampaignName = dr.Field<string>("campaign_name"),
                    CampaignMapName = dr.Field<string>("map_name"),
                    IsTutorial = table_campaigns.Columns.Contains("is_tutorial") ? dr.Field<bool>("is_tutorial") : false,
                };

                CachedCampaigns.Add(dbCampaign);
            }
        }

        private void CacheRoadCosts()
        {
            CachedRoads.Clear();

            foreach (DataRow dr in table_campaign_map_roads.Rows)
            {
                var campaign = dr.Field<string>("campaign");

                int campaignIndex = CachedCampaigns.FindIndex(item => item.CampaignName == campaign);
                if (campaignIndex == -1)
                {
                    continue;
                }

                if (CachedCampaigns[campaignIndex].CampaignMapName != _campaignMapName)
                {
                    continue;
                }

                if (CachedCampaigns[campaignIndex].IsTutorial)
                {
                    continue;
                }

                var dbRoad = new DBRoad()
                {
                    Key = dr.Field<string>("key"),
                    CampaignName = campaign,
                    Threshold = (float)dr.Field<double>("threshold"),
                    MoveCost = (uint)dr.Field<int>("movement_cost"),
                };

                CachedRoads.Add(dbRoad);
            }
        }

        private void CacheRegionsToProvinces()
        {
            CachedRegionsToProvinces.Clear();
            foreach (DataRow dr in table_regions_to_provinces.Rows)
            {
                var region = dr.Field<string>("region");
                var pkey = new string[] { _campaignMapName, region };
                var campaign_map_region = table_campaign_map_regions.Rows.Find(pkey);
                if (campaign_map_region == null)
                {
                    continue;
                }

                var province = dr.Field<string>("province");

                var dbRegionToProvince = new DBRegionToProvince()
                {
                    Region = region,
                    Province = province,
                };

                CachedRegionsToProvinces.Add(dbRegionToProvince);
            }
        }

        private void CacheData(GameTemplate game)
        {
            CacheRegions(game);
            CacheGroundTypes();
            CacheClimates();
            CacheAttritions();
            CacheAreasOfInterest(game);
            CacheCampaigns();
            CacheRoadCosts();
            CacheRegionsToProvinces();
        }

        private bool VerifyRegionIndices(GameTemplate game, MapHexFile mapHexFile)
        {
            if (game != GameTemplate.Pharaoh_Dynasties)
            {
                return true;
            }

            var expectedCombinedIds = new Dictionary<string, int>(CachedRegions.Count);
            foreach (var region in CachedRegions)
            {
                expectedCombinedIds[region.Key] = region.Id;
            }

            return mapHexFile.VerifyRegionIndices(expectedCombinedIds);
        }

        public bool CacheTableData(GameTemplate game, string tableName)
        {
            switch (tableName)
            {
                case Constants.TABLE_CAMPAIGN_MAP_REGIONS:
                case Constants.TABLE_REGIONS:
                    CacheRegions(game);
                    return true;
                case Constants.TABLE_CAMPAIGN_GROUND_TYPES:
                    CacheGroundTypes();
                    return true;
                case Constants.TABLE_CLIMATES:
                    CacheClimates();
                    return true;
                case Constants.TABLE_CAMPAIGN_MAP_ATTRITIONS:
                    CacheAttritions();
                    return true;
                case Constants.TABLE_CAMPAIGN_MAP_AREAS_OF_INTEREST:
                    CacheAreasOfInterest(game);
                    return true;
                case Constants.TABLE_CAMPAIGNS:
                    CacheCampaigns();
                    return true;
                case Constants.TABLE_CAMPAIGN_MAP_ROADS:
                    CacheRoadCosts();
                    return true;
                case Constants.TABLE_REGIONS_TO_PROVINCES:
                    CacheRegionsToProvinces();
                    return true;
            }

            LoggerViewModel.Log($"Database - failed to cache {tableName} table, no such table is known.", LogLevel.Error);
            return false;
        }
    }
}
