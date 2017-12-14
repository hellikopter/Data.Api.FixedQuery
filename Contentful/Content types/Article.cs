﻿namespace Contentful
{
    using System.Collections.Generic;

    [Class("https://id.parliament.uk/schema/Article")]
    [BaseUri("https://id.parliament.uk/")]
    public class Article : ContentfulClass, IUriEntry
    {
        [Predicate("http://example.com/content/schema/title")]
        public string Title { get; set; }

        [Predicate("http://example.com/content/schema/summary")]
        public string Summary { get; set; }

        [Predicate("http://example.com/content/schema/body")]
        public string Body { get; set; }

        [Predicate("http://example.com/content/schema/relatedArticle")]
        public IEnumerable<Article> RelatedArticle { get; set; }

        [Predicate("http://example.com/content/schema/topic")]
        public IEnumerable<Topic> Topic { get; set; }

        [Predicate("http://example.com/content/schema/articleType")]
        public IEnumerable<ArticleType> ArticleType { get; set; }

        [Predicate("http://example.com/content/schema/audience")]
        public IEnumerable<Audience> Audience { get; set; }

        [Predicate("http://example.com/content/schema/publisher")]
        public IEnumerable<Publisher> Publisher { get; set; }

        [Predicate("http://example.com/content/schema/collection")]
        public IEnumerable<Collection> Collection { get; set; }

        public string ParliamentId { get; set; }

        public string Uri => this.ParliamentId;
    }
}