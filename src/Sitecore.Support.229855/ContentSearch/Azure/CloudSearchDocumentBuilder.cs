using Sitecore.ContentSearch.Azure;
using Sitecore.ContentSearch.Azure.Schema;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.Support.ContentSearch.Azure.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Sitecore.Support.ContentSearch.Azure
{
  public class CloudSearchDocumentBuilder : Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder
  {
    private readonly CultureInfo culture;

    public CloudSearchDocumentBuilder(Sitecore.ContentSearch.IIndexable indexable, Sitecore.ContentSearch.IProviderUpdateContext context) : base(indexable, context)
    {
      this.culture = ((indexable != null) ? indexable.Culture : null);
    }

    protected override void AddComputedIndexField(IComputedIndexField computedIndexField, object fieldValue)
    {
      if (fieldValue is IEnumerable && fieldValue.GetType().IsGenericType)
      {
        foreach (var field in fieldValue as IEnumerable)
        {
          this.AddField(computedIndexField.FieldName, field);
        }
      }
      else
      {
        this.AddField(computedIndexField.FieldName, fieldValue);
      }
    }

    protected virtual void AddField(string fieldName, object fieldValue, CloudSearchFieldConfiguration cloudConfiguration, bool append = false)
    {
      if (cloudConfiguration != null && cloudConfiguration.Ignore)
      {
        return;
      }

      var fieldIsEmpty = IsValueNullOrEmpty(fieldValue);

      if (cloudConfiguration != null)
      {
        fieldValue = cloudConfiguration.FormatForWriting(fieldValue);
      }

      if (fieldValue == null)
      {
        return;
      }

      Type cloudFieldType = cloudConfiguration?.Type != null ? cloudConfiguration.Type : fieldValue.GetType();

      string cloudName = this.Index.FieldNameTranslator.GetIndexFieldName(fieldName, cloudFieldType);

      if (cloudName == null)
      {
        CrawlingLog.Log.Warn($"[Index={this.Index.Name}] The index fieldname is translated to null for field '{fieldName}'.");
        return;
      }

      this.AddField(cloudName, fieldValue, cloudConfiguration, append, fieldIsEmpty);

      var cloudIndex = this.Index as CloudSearchProviderIndex;
      var schemaBuilder = cloudIndex.SchemaBuilder as Sitecore.Support.ContentSearch.Azure.Schema.CloudSearchIndexSchemaBuilder;

      // Add field to schema before formatting 
      BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Static;
      FieldInfo field = typeof(Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder).GetField("culture", bindFlags);
      var cultureValue = field.GetValue(this) as CultureInfo;

      var buildedField = schemaBuilder.BuildField(fieldName, fieldValue, cloudConfiguration, cultureValue);

      if (!string.IsNullOrEmpty(buildedField?.Analyzer) && cloudConfiguration?.CloudAnalyzer == "language")
      {
        cloudName = (this.Index.FieldNameTranslator as CloudFieldNameTranslator).GetIndexFieldName(fieldName, cloudFieldType, this.culture);

        this.AddField(cloudName, fieldValue, cloudConfiguration, append, fieldIsEmpty);
      }
    }

    protected override void AddField(string cloudName, object fieldValue, CloudSearchFieldConfiguration cloudConfiguration, bool append, bool fieldIsEmpty)
    {
      if (!append && this.Document.ContainsKey(cloudName))
      {
        return;
      }

      var cloudIndex = this.Index as CloudSearchProviderIndex;
      var schemaBuilder = cloudIndex.SchemaBuilder as Sitecore.Support.ContentSearch.Azure.Schema.CloudSearchIndexSchemaBuilder;

      // Add field to schema before formatting 
      schemaBuilder.AddField(cloudName, fieldValue, cloudConfiguration, this.culture);

      var formattedValue = this.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(fieldValue, cloudName);
      if (formattedValue == null)
      {
        return;
      }

      this.Document.AddOrUpdate(key: cloudName, addValue: formattedValue,
          updateValueFactory:
          (key, existingValue) =>
          {
            if (!fieldIsEmpty && existingValue is string && formattedValue is string)
            {
              return string.Concat(existingValue, " ", formattedValue);
            }

            CrawlingLog.Log.Debug($"[Index={this.Index.Name}] The '{cloudName}' field is skipped: the field already exists in the document and its value is not a string.");
            return existingValue;
          });
    }

    protected static bool IsValueNullOrEmpty(object fieldValue)
    {
      if (fieldValue == null)
      {
        return true;
      }

      string strFieldValue = fieldValue as string;
      return strFieldValue != null && String.Equals(strFieldValue, String.Empty);
    }
  }
}