namespace Sitecore.ContentSearch.Azure.Schema
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Azure.Config;
    using Sitecore.ContentSearch.Azure.FieldMaps;
    using Sitecore.ContentSearch.Azure.Models;
    using Sitecore.ContentSearch.Azure.Analyzers;
    using System.Globalization;
  using Sitecore.ContentSearch.Azure.Schema;
  using Sitecore.ContentSearch.Azure;

  internal class CloudSearchIndexSchemaBuilder : ICloudSearchIndexSchemaBuilder2
    {
        private readonly IDictionary<string, IndexedField> fields;

        private ICloudSearchTypeMapper typeMap;

        private ICloudFieldMap fieldMap;

        private ICloudCultureBasedAnalyzerConfiguration cloudCultureBasedAnalyzerConfiguration;

        public CloudSearchIndexSchemaBuilder()
        {
            this.fields = new ConcurrentDictionary<string, IndexedField>();
        }

        [Obsolete("Use AddFields(param IndexedField[] fileds)")]
        public void AddField(string fieldName, object fieldValue)
        {
            var config = this.fieldMap.GetCloudFieldConfigurationByCloudFieldName(fieldName);
            this.AddField(fieldName, fieldValue, config);
        }

        public virtual void AddField(string fieldName, object fieldValue, CloudSearchFieldConfiguration configuration, CultureInfo culture = null)
        {
            var field = this.BuildField(fieldName, fieldValue, configuration, culture);

            if (field != null)
            {
                this.AddFields(field);
            }
        }

        public void AddFields(params IndexedField[] fileds)
        {
            lock (this)
            {
                foreach (var field in fileds)
                {
                    if (this.fields.ContainsKey(field.Name))
                    {
                        if (!this.IsCompatibleType(field.Type, this.fields[field.Name].Type))
                        {
                            throw new ApplicationException(
                                $"Conflict between type of incomming field '{field.Type}' and field in schema '{this.fields[field.Name].Type}'");
                        }
                    }
                    else
                    {
                        if (!this.fields.ContainsKey(field.Name))
                        {
                            this.fields.Add(field.Name, field);
                        }
                    }
                }
            }
        }

        protected virtual bool IsCompatibleType(string valueType, string storageType)
        {
            if (valueType == "Edm.String" && storageType == "Collection(Edm.String)")
            {
                return true;
            }

            return storageType == valueType;
        }

        public ICloudSearchIndexSchema GetSchema()
        {
            return new CloudSearchIndexSchema(this.fields.Values.ToList());
        }

        public void Initialize(ProviderIndexConfiguration indexConfiguration)
        {
            var configuration = indexConfiguration as CloudIndexConfiguration;
            if (configuration == null)
            {
                throw new NotSupportedException($"Only {typeof(CloudIndexConfiguration).Name} is supported.");
            }

            this.fieldMap = (ICloudFieldMap)configuration.FieldMap;
            this.typeMap = configuration.CloudTypeMapper;
            this.cloudCultureBasedAnalyzerConfiguration = configuration.CloudCultureBasedAnalyzerConfiguration;
        }

        public IndexedField BuildField(string fieldName, object fieldValue, CloudSearchFieldConfiguration config, CultureInfo culture = null)
        {
            if (config?.Ignore == true)
            {
                return null;
            }

            Type clrType;

            if (config?.Type != null)
            {
                clrType = config?.Type;
            }
            else
            {
                if (fieldValue == null)
                {
                    return null;
                }

                clrType = fieldValue.GetType();
            }

            return this.BuildField(fieldName, clrType, config, culture);
        }

        public IndexedField BuildField(string fieldName, Type clrType, CloudSearchFieldConfiguration configuration, CultureInfo culture = null)
        {
            string edmType = this.typeMap.GetEdmTypeName(clrType);

            bool retrievable = configuration?.Retrievable ?? true;
            bool sortable = configuration?.Sortable ?? edmType != "Collection(Edm.String)";
            bool searchable = configuration?.Searchable ?? edmType == "Edm.String" || edmType == "Collection(Edm.String)";
            bool filterable = configuration?.Filterable ?? true;
            bool facetable = configuration?.Facetable ?? true;

            string fieldAnalyzer = configuration?.CloudAnalyzer;
            if (searchable && configuration?.CloudAnalyzer == "language")
            {
                CloudCultureBasedAnalyzer analyzer = this.cloudCultureBasedAnalyzerConfiguration?.GetCultureAnalyzer(culture?.TwoLetterISOLanguageName);
                fieldAnalyzer = analyzer?.Analyzer;
            }

            var isKey = fieldName.Equals(CloudSearchConfig.VirtualFields.CloudUniqueId);
            return new IndexedField(fieldName, edmType, isKey, searchable, retrievable, sortable, filterable, facetable)
            {
                Analyzer = searchable ? fieldAnalyzer : null
            };
        }

        public void Reset()
        {
            this.fields.Clear();
        }

    }
}