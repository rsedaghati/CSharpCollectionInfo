using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;

namespace CollectionInfo;

[Export(typeof(IViewTaggerProvider))]
[ContentType("CSharp")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TagType(typeof(IntraTextAdornmentTag))]//IntraTextAdornmentTag
public sealed class CollectionIndexTaggerProvider : IViewTaggerProvider
{
    public ITagger<T>? CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        //System.Diagnostics.Debug.WriteLine(">>> CollectionIndexTaggerProvider.CreateTagger()");

        // Only create for the primary buffer of a WPF text view
        if (textView is IWpfTextView wpfTextView &&
            buffer == textView.TextBuffer)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                () => new CollectionIndexTagger(wpfTextView)) as ITagger<T>;
        }
        return null;
    }
}
