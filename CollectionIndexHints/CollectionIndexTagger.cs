using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
//using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CollectionInfo;

internal sealed class CollectionIndexTagger : ITagger<IntraTextAdornmentTag>, IDisposable
{
    private readonly IWpfTextView _view;
    private bool _disposed;

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public CollectionIndexTagger(IWpfTextView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));

        _view.Caret.PositionChanged += OnCaretPositionChanged;
        _view.LayoutChanged += OnLayoutChanged;
        _view.TextBuffer.Changed += OnBufferChanged;
    }

    public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (_disposed || spans == null || spans.Count == 0)
            yield break;

        var snapshot = spans[0].Snapshot;
        var caretPoint = _view.Caret.Position.BufferPosition;

        // Map caret to the snapshot we are tagging
        if (caretPoint.Snapshot != snapshot)
        {
            caretPoint = caretPoint.TranslateTo(snapshot, PointTrackingMode.Positive);
        }

        if (!TryGetCollectionElements(snapshot, caretPoint.Position, out List<TextSpan>? elementSpans))
            yield break;

        int index = 0;
        foreach (TextSpan elementSpan in elementSpans)
        {
            // Zero-length span at the start of the element
            var tagSpan = new SnapshotSpan(snapshot, elementSpan.Start, 0);

            System.Windows.UIElement? adornment = CreateIndexAdornment(index);

            var tag = new IntraTextAdornmentTag(
                adornment,
                removalCallback: null,
                topSpace: null,
                baseline: null,
                textHeight: null,
                bottomSpace: null,
                affinity: PositionAffinity.Predecessor);

            yield return new TagSpan<IntraTextAdornmentTag>(tagSpan, tag);
            index++;
        }
    }

    private static System.Windows.UIElement CreateIndexAdornment(int index)
    {
        //var color = System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88);
        //double Y = 0.2126 * color.ScR + 0.7152 * color.ScG + 0.0722 * color.ScB;
        //Color backColor = Y > 0.4 ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255);
        return new TextBlock
        {
            Text = index.ToString(),
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 240)),
            Background = new SolidColorBrush(Color.FromRgb(80,88,108)),
            Margin = new System.Windows.Thickness(0, 0, 4, 0),
            Opacity = 0.75,
            IsHitTestVisible = false,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
    }

    /// <summary>
    /// Returns the spans of the individual elements if the caret is inside
    /// a collection initializer or collection expression.
    /// </summary>
    private bool TryGetCollectionElements(ITextSnapshot snapshot, int caretPosition, out List<TextSpan>? elementSpans)
    {
        elementSpans = null;

        // Get Roslyn Document
        var document = snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
        if (document == null)
            return false;

        // Prefer the current solution's version of the document
        document = document.Project.Solution.GetDocument(document.Id) ?? document;

        SyntaxNode? root;
        try
        {
            // Synchronous for simplicity in a first extension.
            // For production you may want to cache or use async carefully.
            _ = document.TryGetSyntaxRoot(out root);//.GetSyntaxRootAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return false;
        }

        if (root == null)
            return false;

        var token = root.FindToken(caretPosition);
        var node = token.Parent;

        // Walk up looking for a collection initializer or collection expression
        while (node != null)
        {
            if (node is InitializerExpressionSyntax initializer)
            {
                // Only treat array / collection initializers
                if (initializer.IsKind(SyntaxKind.ArrayInitializerExpression) ||
                    initializer.IsKind(SyntaxKind.CollectionInitializerExpression) ||
                    initializer.IsKind(SyntaxKind.ComplexElementInitializerExpression))
                {
                    if (IsCaretInside(initializer.Span, caretPosition))
                    {
                        elementSpans = initializer.Expressions
                            .Select(e => e.Span)
                            .ToList();
                        return elementSpans.Count > 0;
                    }
                }
            }
            else if (node is CollectionExpressionSyntax collectionExpr) // C# 12+
            {
                if (IsCaretInside(collectionExpr.Span, caretPosition))
                {
                    elementSpans = collectionExpr.Elements
                        .Select(e => e.Span)
                        .ToList();
                    return elementSpans.Count > 0;
                }
            }

            node = node.Parent;
        }

        return false;
    }

    private static bool IsCaretInside(TextSpan span, int position)
    {
        // Inclusive of the braces/brackets
        return position >= span.Start && position <= span.End;
    }

    private void RaiseTagsChanged()
    {
        var snapshot = _view.TextSnapshot;
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
    }

    private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
    {
        RaiseTagsChanged();
    }

    private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
    {
        if (e.NewSnapshot != e.OldSnapshot)
            RaiseTagsChanged();
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        RaiseTagsChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _view.Caret.PositionChanged -= OnCaretPositionChanged;
        _view.LayoutChanged -= OnLayoutChanged;
        _view.TextBuffer.Changed -= OnBufferChanged;
    }
}
