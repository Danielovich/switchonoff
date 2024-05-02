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
        // post property.
        while ((line = reader.ReadLine()) != null)
        {
            if (!(line.StartsWith(validMarkdownComment1) || line.StartsWith(validMarkdownComment2)))
            {
                break;
            }

            if (line.StartsWith(validMarkdownComment1) || line.StartsWith(validMarkdownComment2))
            {
                var markdownComment = new MarkdownComment(line);

                // I don't know about this construct, it blurs the image somewhat as to why 
                // we want to check for null before we populate the property.
                // but it is necessary since we are in a loop that will override the property
                // if we do not check. And since our property is not nullable we cannot simply use
                // ??=. I do not want to change the property signature just because it makes it easier
                // in this case.
                MarkdownPost.Title = MarkdownPost.Title.NullIfEmpty() ?? markdownComment.Title();
                MarkdownPost.Slug = MarkdownPost.Slug.NullIfEmpty() ?? markdownComment.Slug();

                // I am stopping this implementation here! Because I can see where this is going now.
                // The default value of the PubDate is DateTime.UtcNow which would leave us with a poor 
                // choice for checking a value that might have been set prior in the loop.
                // And since I have no idea where the MarkdownPost is used elsewhere, I find 
                // changing the signature a bad idea. Who knows what changing the property would cause elsewhere ?

                // What ? This ? no way
                // MarkdownPost.PubDate = if markdownComment.PubDate() is between a narrow interval
                // it would have been nice to be able to do 
                // MarkdownPost.PubDate ??= markdownComment.PubDate();

                MarkdownPost.PubDate = markdownComment.PubDate();


                MarkdownPost.LastModified = markdownComment.LastModified();
                MarkdownPost.Excerpt = markdownComment.Excerpt();
                MarkdownPost.Categories = markdownComment.Categories();
                MarkdownPost.IsPublished = markdownComment.IsPublished();
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


//I had to use these because the string properties in the MarkdownPost are not nullable,
//which is by design. And I am not changing them just because an initial switch statement looked off.
public static class StringExtensions
{
    public static string NullIfEmpty(this string s)
    {
        return string.IsNullOrEmpty(s) ? null : s;
    }
    public static string NullIfWhiteSpace(this string s)
    {
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}

public class MardownFile
{
    public MardownFile() {}

    public MardownFile(string contents)
    {
        Contents = contents;
    }

    public string Contents { get; set; } = string.Empty;
}

// I have come to like this approach simply because it encapsulates the 
// ownership of the Properties that might available inside a MarkdownComment
public class MarkdownComment
{
    public MarkdownComment(string comment)
    {
        Comment = comment;
    }

    public string Comment { get; set; }

    public string Title()
    {
        return this.AsPostProperty("title").Value;
    }

    public string Slug()
    {
        return this.AsPostProperty("slug").Value;
    }

    public DateTime PubDate()
    {
        return this.AsPostProperty("pubDate").ParseToDate();
    }

    internal DateTime LastModified()
    {
        return this.AsPostProperty("lastModified").ParseToDate();
    }

    internal string Excerpt()
    {
        return this.AsPostProperty("excerpt").Value;
    }

    internal IList<string> Categories()
    {
        return this.ToProperty("categories", ',').Select(n => n.Value).ToList();
    }

    internal bool IsPublished()
    {
        return this.AsPostProperty("isPublished").ParseToBool();
    }
}

public class MarkdownPost
{
    // I changed this property which goes against my own statement about not changing the design
    // of the type here. Simply because we do not know where it is being used.
    public virtual IList<string> Categories { get; set; } = new List<string>();
    public virtual IList<string> Tags { get; } = new List<string>();
    public virtual string Content { get; set; } = string.Empty;
    public virtual string Excerpt { get; set; } = string.Empty;
    public virtual bool IsPublished { get; set; } = false;
    public virtual DateTime LastModified { get; set; } = DateTime.UtcNow;
    public virtual DateTime PubDate { get; set; } = DateTime.UtcNow; // yikes
    public virtual string Slug { get; set; } = string.Empty;
    public virtual string Title { get; set; } = string.Empty;
}

public static class MarkdownCommentExtensions
{
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
