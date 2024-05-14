
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

    // A different approach. I would call it a little more clever in the sense that it compress the original switch
    // statement and uses a data structure in a way that is not very common (Action as TValue!).
    // But it's not pretty I think and it does compress the switch into something I find less readable.
    // On the other hand I have not touched any other code other than the caller for this dictionary, and
    // that's a good thing.
    public Dictionary<string, Action<MarkdownComment>> markdownCommentParseActions =>
        new Dictionary<string, Action<MarkdownComment>>
        {
            ["title"] = mc => MarkdownPost.Title = mc.AsPostProperty("title").Value,
            ["slug"] = mc => MarkdownPost.Slug = mc.AsPostProperty("slug").Value,
            ["pubDate"] = mc => MarkdownPost.PubDate = mc.AsPostProperty("pubDate").ParseToDate(),
            ["lastModified"] = mc => MarkdownPost.LastModified = mc.AsPostProperty("lastModified").ParseToDate(),
            ["excerpt"] = mc => MarkdownPost.Excerpt = mc.AsPostProperty("excerpt").Value,
            ["categories"] = mc => mc.ToProperty("categories", ',').Select(n => n.Value).ToList().ForEach(MarkdownPost.Categories.Add),
            ["isPublished"] = mc => MarkdownPost.IsPublished = mc.AsPostProperty("isPublished").ParseToBool()
        };

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

            var markdownComment = new MarkdownComment(line);

            string commentValue = markdownComment.GetValueBetweenFirstQuoteAndColon();

            // Check if the property key exists in the dictionary and execute the corresponding action
            if (markdownCommentParseActions.TryGetValue(commentValue, out var action))
            {
                action(markdownComment);
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

        Assert.True(markDownBlogpostParser.MarkdownPost.Title == "hugga bugga ulla johnson");
        Assert.True(markDownBlogpostParser.MarkdownPost.Slug == "hulla bulla");
        Assert.True(markDownBlogpostParser.MarkdownPost.PubDate == new DateTime(2017, 10, 13, 18, 59, 00));
        Assert.True(markDownBlogpostParser.MarkdownPost.LastModified == new DateTime(2017, 10, 13, 23, 59, 00));
        Assert.True(markDownBlogpostParser.MarkdownPost.Excerpt == "an excerpt you would never imagine ");
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
