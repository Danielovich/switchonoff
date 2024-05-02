
public class MarkdownPostParser
{
    private const string validMarkdownComment1 = "[//]: #";
    private const string validMarkdownComment2 = "[//]:#";
    public string Markdown { get; set; } = string.Empty;

    public MarkdownPostParser(MardownFile markdownFile)
    {
        ArgumentNullException.ThrowIfNull(markdownFile.Contents);

        Markdown = markdownFile.Contents;
        MarkdownPost = new MarkdownPost();
    }

    public MarkdownPost MarkdownPost { get; private set; }


    public async Task ParseCommentsAsPropertiesAsync()
    {
        using var reader = new StringReader(Markdown);

        // line of content from the Markdown
        string? line;

        // Convention is that all comments from top of the MarkDown is a future
        // post property. As soon as the parser sees something else it breaks.
        while ((line = reader.ReadLine()) != null)
        {
            if (!(line.StartsWith(validMarkdownComment1) || line.StartsWith(validMarkdownComment2)))
            {
                break;
            }

            if (line.StartsWith(validMarkdownComment1) || line.StartsWith(validMarkdownComment2))
            {
                var markdownComment = new MarkdownComment(line);

                // somewhat a tedious bit of code but it's easy to understand
                // we start by looking at what type of comment we have on our hands
                // from there we extract the comment value 
                switch (markdownComment.GetValueBetweenFirstQuoteAndColon())
                {
                    case "title":
                        MarkdownPost.Title = markdownComment.AsPostProperty("title").Value;
                        break;
                    case "slug":
                        MarkdownPost.Slug = markdownComment.AsPostProperty("slug").Value;
                        break;
                    case "pubDate":
                        MarkdownPost.PubDate = markdownComment.AsPostProperty("pubDate").ParseToDate();
                        break;
                    case "lastModified":
                        MarkdownPost.LastModified = markdownComment.AsPostProperty("lastModified").ParseToDate();
                        break;
                    case "excerpt":
                        MarkdownPost.Excerpt = markdownComment.AsPostProperty("excerpt").Value;
                        break;
                    case "categories":
                        markdownComment.ToProperty("categories", ',').Select(n => n.Value).ToList().ForEach(MarkdownPost.Categories.Add);
                        break;
                    case "isPublished":
                        MarkdownPost.IsPublished = markdownComment.AsPostProperty("isPublished").ParseToBool();
                        break;
                    default:
                        break;
                }
            }
        }

        await Task.CompletedTask;
    }
}

#region tests

public class MarkdownCommentsToBlogpostPropertiesTests
{
    [Fact]
    public async Task Parse_Comments_As_Blog_Properties()
    {
        var comments =
              "[//]: # \"title: hugga bugga ulla johnson\"\n" +
              "[//]: # \"johnny: hugga bugga ulla johnson\"";

        var markDownBlogpostParser = new MarkdownPostParser(new MardownFile(comments));
        await markDownBlogpostParser.ParseCommentsAsPropertiesAsync();

        Assert.Equal("hugga bugga ulla johnson", markDownBlogpostParser.MarkdownPost.Title);
    }

    [Fact]
    public async Task Parse_All_Comments_To_Post_Properties()
    {
        var comments =
              "[//]: # \"title: hugga bugga ulla johnson\" \n" +
              "[//]: # \"slug: hulla bulla\" \n" +
              "[//]: # \"pubDate: 13/10/2017 18:59\"\n" +
              "[//]: # \"lastModified: 13/10/2017 23:59\"\n" +
              "[//]: # \"excerpt: an excerpt you would never imagine \"\n" +
              "[//]: # \"categories: cars, coding, personal, recipes \"\n" +
              "[//]: # \"isPublished: true \"";

        var markDownBlogpostParser = new MarkdownPostParser(new MardownFile(comments));
        await markDownBlogpostParser.ParseCommentsAsPropertiesAsync();

        Assert.True(markDownBlogpostParser.MarkdownPost.Title != string.Empty);
        Assert.True(markDownBlogpostParser.MarkdownPost.Slug != string.Empty);
        Assert.True(markDownBlogpostParser.MarkdownPost.PubDate > DateTime.MinValue);
        Assert.True(markDownBlogpostParser.MarkdownPost.LastModified > DateTime.MinValue);
        Assert.True(markDownBlogpostParser.MarkdownPost.Excerpt != string.Empty);
        Assert.True(markDownBlogpostParser.MarkdownPost.Categories.Count() == 4);
        Assert.True(markDownBlogpostParser.MarkdownPost.IsPublished);
    }
}

#endregion


#region code for primary objectives to work

public class MardownFile
{
    public MardownFile() {}

    public MardownFile(string contents)
    {
        Contents = contents;
    }

    public string Contents { get; set; } = string.Empty;
}

// Represents a full markdown comment.
// Such as [//]: # "categories: ugga, bugga, johnny"
public class MarkdownComment
{
    public MarkdownComment(string comment)
    {
        Comment = comment;
    }

    public string Comment { get; set; }
}

public class MarkdownPost
{
    public virtual IList<string> Categories { get; } = new List<string>();
    public virtual IList<string> Tags { get; } = new List<string>();
    public virtual string Content { get; set; } = string.Empty;
    public virtual string Excerpt { get; set; } = string.Empty;
    public virtual bool IsPublished { get; set; } = false;
    public virtual DateTime LastModified { get; set; } = DateTime.UtcNow;
    public virtual DateTime PubDate { get; set; } = DateTime.UtcNow;
    public virtual string Slug { get; set; } = string.Empty;
    public virtual string Title { get; set; } = string.Empty;
}

public static class MarkdownCommentExtensions
{
    public static string GetValueBetweenFirstQuoteAndColon(this MarkdownComment comment)
    {
        // Regex pattern to find the value between the first quote and the first colon
        string pattern = "\"(.*?):";

        Match match = Regex.Match(comment.Comment, pattern);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return string.Empty;
    }


    public static MarkdownProperty AsPostProperty(this MarkdownComment commentAsProperty, string commentProperty)
    {
        // it exercises on a comment like this [//]: # "title: hugga bugga ulla johnson"
        var pattern = $"{commentProperty}:\\s*(.*?)\"";

        Match match = Regex.Match(commentAsProperty.Comment, pattern);
        if (match.Success)
        {
            return new MarkdownProperty { Value = match.Groups[1].Value };
        }

        return new MarkdownProperty();
    }

    public static IEnumerable<MarkdownProperty> ToProperty(this MarkdownComment commentAsProperty, string commentProperty, char delimeter)
    {
        // it exercises on a comment that reflects a list of values,
        // e.g [//]: # "categories: ugga, bugga, johnny"
        var pattern = $"{commentProperty}:\\s*(.*?)\"";

        Match match = Regex.Match(commentAsProperty.Comment, pattern);
        if (match.Success)
        {
            var listOfValues = match.Groups[1].Value.Split(delimeter);

            foreach (var value in listOfValues)
            {
                yield return new MarkdownProperty { Value = value };
            }
        }
    }
}

// Represents a Value of 
public class MarkdownProperty
{
    public string Value { get; set; }
}


public static class MarkdownPropertyExtensions
{
    public static DateTime ParseToDate(this MarkdownProperty markdownProperty)
    {
        var formats = new string[] {
            "dd/MM/yyyy HH:mm",
            "d/MM/yyyy HH:mm",
            "dd/M/yyyy HH:mm",
            "d/M/yyyy HH:mm" };

        if (DateTime.TryParseExact(
            markdownProperty.Value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date))
        {
            return date;
        }

        return DateTime.MinValue;
    }

    public static bool ParseToBool(this MarkdownProperty markdownProperty)
    {
        return bool.TryParse(markdownProperty.Value, out bool result) ? result : false;
    }
}
#endregion
