namespace Sitecore.Support.ContentSearch.Azure
{
  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.ComputedFields;

  public class CloudSearchDocumentBuilder : Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder
  {

    protected override void AddComputedIndexField(IComputedIndexField computedIndexField, object fieldValue)
    {
      this.AddField(computedIndexField.FieldName, fieldValue, false);
    }

    public CloudSearchDocumentBuilder(IIndexable indexable, IProviderUpdateContext context) : base(indexable, context)
    {
    }
  }
}
