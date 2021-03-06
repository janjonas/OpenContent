﻿using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Satrabel.OpenContent.Components.Lucene.Config;
using System;

namespace Satrabel.OpenContent.Components.Lucene.Mapping
{
    public static class JsonMappingUtils
    {
        #region Consts
        /// <summary>
        /// The name of the field which holds the type.
        /// </summary>
        public static readonly string FieldType = "$type";
        /// <summary>
        /// The name of the field which holds the JSON-serialized source of the object.
        /// </summary>
        public static readonly string FieldSource = "$source";

        /// <summary>
        /// The name of the field which holds the timestamp when the document was created.
        /// </summary>
        public static readonly string FieldTimestamp = "$timestamp";
        public static readonly string FieldId = "$id";
        #endregion

        public static Document JsonToDocument(string type, string id, string source, FieldConfig config, bool storeSource = false)
        {
            var objectMapper = new JsonObjectMapper();
            Document doc = new Document();
            string json = source;  //JsonConvert.SerializeObject(source, typeof(TSource), settings);
            doc.Add(new Field(FieldType, type, Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field(FieldId, id, Field.Store.YES, Field.Index.NOT_ANALYZED));
            if (storeSource)
            {
                doc.Add(new Field(FieldSource, json, Field.Store.YES, Field.Index.NO));
            }
            doc.Add(new NumericField(FieldTimestamp, Field.Store.YES, true).SetLongValue(DateTime.UtcNow.Ticks));
            objectMapper.AddJsonToDocument(source, doc, config);
            return doc;
        }

        public static Filter GetTypeFilter(string type)
        {
            var typeTermQuery = new TermQuery(new Term(FieldType, type));
            BooleanQuery query = new BooleanQuery();
            query.Add(typeTermQuery, Occur.MUST);
            Filter filter = new QueryWrapperFilter(query);
            return filter;
        }
        public static Filter GetTypeFilter(string type, Query filter)
        {
            var typeTermQuery = new TermQuery(new Term(FieldType, type));
            BooleanQuery query = new BooleanQuery();
            query.Add(typeTermQuery, Occur.MUST);
            query.Add(filter, Occur.MUST);
            Filter resultFilter = new QueryWrapperFilter(query);
            return resultFilter;
        }

        public static Analyzer GetAnalyser()
        {
            var analyser = new StandardAnalyzer(global::Lucene.Net.Util.Version.LUCENE_30);
            return analyser;
        }
    }
}
