﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Syntax;
using Microsoft.CodeAnalysis.Text;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Graphics.Shapes;
using Markdig.Syntax.Inlines;
using osu.Framework.Graphics.Animations;
using OpenTK;
using OpenTK.Graphics;

namespace osu.Framework.Markdown.Tests.Visual
{
    [System.ComponentModel.Description("frame-based animations")]
    public class TestCaseMarkdown : TestCase
    {
        static void Error(string message)
        {
            Console.WriteLine(message);
            Environment.Exit(1);
        }

        public TestCaseMarkdown()
        {
            var path = "https://github.com/lunet-io/scriban/blob/master/doc/language.md";
            string markdown = null;
            if (path.StartsWith("https:"))
            {
                Uri uri;
                if (!Uri.TryCreate(path, UriKind.Absolute, out uri))
                {
                    Error($"Unable to parse Uri `{path}`");
                    return;
                }
                // Special handling of github URL to access the raw content instead
                if (uri.Host == "github.com")
                {
                    // https://github.com/lunet-io/scriban/blob/master/doc/language.md
                    // https://raw.githubusercontent.com/lunet-io/scriban/master/doc/language.md
                    var newPath = uri.AbsolutePath;
                    var paths = new List<string>(newPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
                    if (paths.Count < 5 || paths[2] != "blob")
                    {
                        Error($"Invalid github.com URL `{path}`");
                        return;
                    }
                    paths.RemoveAt(2); // remove blob
                    uri = new Uri($"https://raw.githubusercontent.com/{(string.Join("/", paths))}");
                }

                var httpClient = new HttpClient();
                markdown = httpClient.GetStringAsync(uri).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                markdown = File.ReadAllText(path);
            }

            var container = new MarkdownScrollContainer()
            {
                RelativeSizeAxes = Axes.Both,
            };
            
            /*
             markdown= @"- [1. Blocks](#1-blocks)
  - [1.1 Code block](#11-code-block)
  - [1.2 Text block](#12-text-block)
  - [1.3 Escape block](#13-escape-block)
  - [1.4 Whitespace control](#14-whitespace-control)";
             */

            container.MarkdownText = markdown;
            

            Add(container);
        }
    }

    /// <summary>
    ///     MarkDownScrollContainer
    /// </summary>
    public class MarkdownScrollContainer : ScrollContainer
    {
        public MarkdownDocument MarkdownDocument
        {
            get => _markdownContainer.MarkdownDocument;
            set => _markdownContainer.MarkdownDocument = value;
        }

        public string MarkdownText
        {
            get => _markdownContainer.MarkdownText;
            set => _markdownContainer.MarkdownText = value;
        }

        private readonly MarkdownContainer _markdownContainer;

        public MarkdownScrollContainer()
        {
            ScrollbarOverlapsContent = false;
            Child = _markdownContainer = new MarkdownContainer
            {
                Padding = new MarginPadding(3),
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X
            };
        }
    }

    /// <summary>
    ///     MarkdownContainer
    ///     Contains  all the markdown component <see cref="IMarkdownObject" /> in <see cref="MarkdownDocument" />
    /// </summary>
    public class MarkdownContainer : FillFlowContainer
    {
        private const int seperator_px = 25;

        public MarkdownDocument MarkdownDocument
        {
            get => _document;
            set
            {
                _document = value;
                //clear all exist markdown object
                Clear();

                //start creating
                const int root_layer_index = 0;

                foreach (var component in _document)
                    AddMarkdownComponent(component, this, root_layer_index);
            }
        }

        public string MarkdownText
        {
            //TODO : get value from MarkdownDocument
            get => "";
            set
            {
                var markdownText = value;
                var pipeline = new MarkdownPipelineBuilder().UseAutoIdentifiers(AutoIdentifierOptions.GitHub).Build();
                MarkdownDocument = Markdown.Parse(markdownText, pipeline);
            }
        }

        private MarkdownDocument _document;

        public MarkdownContainer()
        {
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(seperator_px);
            Margin = new MarginPadding { Left = 20, Right = 20 };
        }

        public void AddMarkdownComponent(IMarkdownObject markdownObject, FillFlowContainer container, int layerIndex)
        {
            if (markdownObject is HeadingBlock headingBlock)
            {
                container.Add(new MarkdownHeadingBlock(headingBlock));
            }
            else if (markdownObject is LiteralInline literalInline)
            {
                container.Add(new MarkdownSeperator(literalInline));
            }
            else if (markdownObject is ParagraphBlock paragraphBlock)
            {
                var drawableParagraphBlock = new MarkdownTextFlowContainer();
                switch (layerIndex)
                {
                    case 1:
                        drawableParagraphBlock.AddText("@ ", t => t.Colour = Color4.DarkGray);
                        break;
                    case 2:
                        drawableParagraphBlock.AddText("# ", t => t.Colour = Color4.DarkGray);
                        break;
                    case 3:
                        drawableParagraphBlock.AddText("+ ", t => t.Colour = Color4.DarkGray);
                        break;
                    case 4:
                        drawableParagraphBlock.AddText("+ ", t => t.Colour = Color4.DarkGray);
                        break;
                }

                drawableParagraphBlock = ParagraphBlockHelper.GeneratePartial(drawableParagraphBlock, paragraphBlock.Inline);
                container.Add(drawableParagraphBlock);
            }
            else if (markdownObject is QuoteBlock quoteBlock)
            {
                container.Add(new MarkdownQuoteBlock(quoteBlock));
            }
            else if (markdownObject is FencedCodeBlock fencedCodeBlock)
            {
                container.Add(new MarkdownFencedCodeBlock(fencedCodeBlock));
            }
            else if (markdownObject is ListBlock listBlock)
            {
                var childContainer = new FillFlowContainer()
                {
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10, 10),
                    Margin = new MarginPadding() { Left = 25, Right = 10 },
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                };
                container.Add(childContainer);
                foreach (var single in listBlock)
                {
                    AddMarkdownComponent(single, childContainer, layerIndex + 1);
                }
            }
            else if (markdownObject is ListItemBlock listItemBlock)
            {
                foreach (var single in listItemBlock)
                {
                    AddMarkdownComponent(single, container, layerIndex);
                }
            }
            else
            {
                container.Add(new NotExistMarkdown(markdownObject));
            }

            //show seperator line
            if (markdownObject is LeafBlock leafBlock && !(markdownObject is ParagraphBlock))
            {
                if (leafBlock.Inline != null)
                {
                    container.Add(new MarkdownSeperator(null));
                }
            }
        }
    }

    /// <summary>
    ///     NotExistMarkdown :
    ///     shows the <see cref="IMarkdownObject" /> does not implement in drawable object
    /// </summary>
    internal class NotExistMarkdown : SpriteText
    {
        public NotExistMarkdown(IMarkdownObject markdownObject)
        {
            Colour = new Color4(255, 0, 0, 255);
            TextSize = 21;
            Text = markdownObject?.GetType() + " Does not be implemented";
        }
    }

    /// <summary>
    ///     MarkdownFencedCodeBlock :
    ///     ```
    ///     foo
    ///     ```
    /// </summary>
    internal class MarkdownFencedCodeBlock : Container
    {
        private readonly MarkdownTextFlowContainer _textFlowContainer;

        public MarkdownFencedCodeBlock(FencedCodeBlock fencedCodeBlock)
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Gray,
                    Alpha = 0.5f
                },
                _textFlowContainer = new MarkdownTextFlowContainer
                {
                    Margin = new MarginPadding { Left = 10, Right = 10, Top = 10, Bottom = 10 },
                }
            };

            var lines = fencedCodeBlock.Lines.Lines.Take(fencedCodeBlock.Lines.Count);
            foreach (var sligneLine in lines)
            {
                var lineString = sligneLine.ToString();
                _textFlowContainer.AddParagraph(lineString);
            }
        }
    }

    /// <summary>
    ///     NotExistMarkdown :
    ///     - [1. Blocks](#1-blocks)
    ///     - [1.1 Code block](#11-code-block)
    ///     - [1.2 Text block](#12-text-block)
    ///     - [1.3 Escape block](#13-escape-block)
    ///     - [1.4 Whitespace control](#14-whitespace-control)
    /// </summary>
    internal class MarkdownListBlock : FillFlowContainer
    {
        public MarkdownListBlock(ListBlock listBlock)
        {
            Direction = FillDirection.Vertical;
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;

            const int root_layer_index = 1;
            createLayer(listBlock, root_layer_index);
        }

        private void createLayer(ListBlock listBlock, int layerIndex)
        {
            foreach (var singleBlock in listBlock)
                //TODO : singleBlock has two child
                //[0] : 1. Blocks
                //[1] : 1.1 Code block
                //      1.2 Text block
                //      1.3 Escape block
                //      1.4 Whitespace control

                if (singleBlock is ListItemBlock listitemBlock)
                    foreach (var block in listitemBlock)
                        if (block is ParagraphBlock paragraphBlock)
                        {
                            var drawableParagraphBlock = new MarkdownTextFlowContainer();
                            drawableParagraphBlock.Margin = new MarginPadding { Left = 20 * layerIndex };

                            switch (layerIndex)
                            {
                                case 1:
                                    drawableParagraphBlock.AddText("@ ", t => t.Colour = Color4.DarkGray);
                                    break;
                                case 2:
                                    drawableParagraphBlock.AddText("# ", t => t.Colour = Color4.DarkGray);
                                    break;
                                case 3:
                                    drawableParagraphBlock.AddText("+ ", t => t.Colour = Color4.DarkGray);
                                    break;
                                case 4:
                                    drawableParagraphBlock.AddText("+ ", t => t.Colour = Color4.DarkGray);
                                    break;
                            }

                            drawableParagraphBlock = ParagraphBlockHelper.GeneratePartial(drawableParagraphBlock, paragraphBlock.Inline);
                            Add(drawableParagraphBlock);
                        }
                        else if (block is ListBlock listBlock2)
                        {
                            createLayer(listBlock2, layerIndex + 1);
                        }
        }
    }

    /// <summary>
    ///     MarkdownHeading :
    ///     #Heading1
    ///     ##Heading2
    ///     ###Heading3
    ///     ###3Heading4
    /// </summary>
    internal class MarkdownHeadingBlock : Container
    {
        private readonly MarkdownTextFlowContainer _textFlowContainer;

        public MarkdownHeadingBlock(HeadingBlock headingBlock)
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;

            Children = new Drawable[]
            {
                _textFlowContainer = new MarkdownTextFlowContainer
                {
                }
            };

            var level = headingBlock.Level;
            Vector2 scale = new Vector2(1);

            switch (level)
            {
                case 1:
                    scale = new Vector2(2.7f);
                    break;
                case 2:
                    scale = new Vector2(2);
                    break;
                case 3:
                    scale = new Vector2(1.5f);
                    break;
                case 4:
                    scale = new Vector2(1.3f);
                    break;
                case 5:
                    scale = new Vector2(1);
                    break;
            }

            _textFlowContainer.Scale = scale;
            _textFlowContainer = ParagraphBlockHelper.GeneratePartial(_textFlowContainer, headingBlock.Inline);
        }
    }

    /// <summary>
    ///     MarkdownQuoteBlock :
    ///     > NOTE: This document does not describe the `liquid` language. Check the [`liquid
    ///     website`](https://shopify.github.io/liquid/) directly.
    /// </summary>
    internal class MarkdownQuoteBlock : Container
    {
        private readonly MarkdownTextFlowContainer _textFlowContainer;
        private Box _quoteBox;

        public MarkdownQuoteBlock(QuoteBlock quoteBlock)
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;

            Children = new Drawable[]
            {
                _quoteBox = new Box
                {
                    Colour = Color4.Gray,
                    Width = 5,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.Y
                },
                _textFlowContainer = new MarkdownTextFlowContainer
                {
                    Margin = new MarginPadding { Left = 20 }
                }
            };

            if (quoteBlock.LastChild is ParagraphBlock paragraphBlock)
                _textFlowContainer = ParagraphBlockHelper.GeneratePartial(_textFlowContainer, paragraphBlock.Inline);
        }
    }

    /// <summary>
    ///     MarkdownSeperator :
    ///     (spacing)
    /// </summary>
    internal class MarkdownSeperator : Box
    {
        public MarkdownSeperator(LiteralInline literalInline)
        {
            RelativeSizeAxes = Axes.X;
            Height = 1;
            Colour = Color4.Gray;
        }
    }

    /// <summary>
    ///     Fill <see cref="Inline" /> into <see cref="TextFlowContainer" />
    /// </summary>
    internal static class ParagraphBlockHelper
    {
        public static MarkdownTextFlowContainer GenerateText(ParagraphBlock paragraphBlock)
        {
            var textFlowContainer = new MarkdownTextFlowContainer();
            GeneratePartial(textFlowContainer, paragraphBlock.Inline);
            return textFlowContainer;
        }

        public static MarkdownTextFlowContainer GeneratePartial(MarkdownTextFlowContainer textFlowContainer, ContainerInline lnline)
        {
            foreach (var single in lnline)
            {
                if (single is LiteralInline literalInline)
                {
                    var text = literalInline.Content.ToString();
                    if (lnline.GetNext(literalInline) is HtmlInline
                        && lnline.GetPrevious(literalInline) is HtmlInline htmlInline)
                    {
                        textFlowContainer.AddText(text, t => t.Colour = Color4.MediumPurple);
                    }
                    else if (lnline.GetNext(literalInline) is HtmlEntityInline htmlEntityInline)
                    {
                        textFlowContainer.AddText(text, t => t.Colour = Color4.GreenYellow);
                    }
                    else if (literalInline.Parent is LinkInline linkInline)
                    {
                        textFlowContainer.AddText(text, t => t.Colour = Color4.DodgerBlue);
                    }
                    else
                        textFlowContainer.AddText(text);
                }
                else if (single is CodeInline codeInline)
                {
                    textFlowContainer.AddText(codeInline.Content, t => t.Colour = Color4.Orange);
                }
                else if (single is EmphasisInline emphasisInline)
                {
                    //foreach (var child in emphasisInline)
                    //{
                    //    textFlowContainer.AddText(child.ToString());
                    //}
                }
                else if (single is LinkInline || single is HtmlInline || single is HtmlEntityInline)
                {
                    //DO nothing
                }
                else if (single is LineBreakInline)
                {
                    //IDK what is this but just ignore
                }
                else
                {
                    textFlowContainer.AddText(single.GetType().ToString(), t => t.Colour = Color4.Red);
                }

                //generate child
                if (single is ContainerInline containerInline) GeneratePartial(textFlowContainer, containerInline);
            }

            return textFlowContainer;
        }
    }

    internal class MarkdownTextFlowContainer : TextFlowContainer
    {
        public MarkdownTextFlowContainer()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }
    }

    /// <summary>
    /// List extension
    /// </summary>
    internal static class ListExtension
    {
        public static T GetNext<T>(this IEnumerable<T> guidList, T current)
        {
            return guidList.SkipWhile(i => !i.Equals(current)).Skip(1).FirstOrDefault();
        }

        public static T GetPrevious<T>(this IEnumerable<T> guidList, T current)
        {
            return guidList.TakeWhile(i => !i.Equals(current)).LastOrDefault();
        }
    }
}
