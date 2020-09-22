﻿using Lucene.Net.QueryParsers.Flexible.Core.Messages;
using Lucene.Net.QueryParsers.Flexible.Core.Nodes;
using Lucene.Net.QueryParsers.Flexible.Messages;
using Lucene.Net.QueryParsers.Flexible.Standard.Parser;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lucene.Net.QueryParsers.Flexible.Core.Builders
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /// <summary>
    /// This class should be used when there is a builder for each type of node.
    /// 
    /// <para>
    /// The type of node may be defined in 2 different ways: - by the field name,
    /// when the node implements the <see cref="IFieldableNode"/> interface - by its class,
    /// it keeps checking the class and all the interfaces and classes this class
    /// implements/extends until it finds a builder for that class/interface
    /// </para>
    /// <para>
    /// This class always check if there is a builder for the field name before it
    /// checks for the node class. So, field name builders have precedence over class
    /// builders.
    /// </para>
    /// <para>
    /// When a builder is found for a node, it's called and the node is passed to the
    /// builder. If the returned built object is not <c>null</c>, it's tagged
    /// on the node using the tag <see cref="QueryTreeBuilder.QUERY_TREE_BUILDER_TAGID"/>.
    /// </para>
    /// <para>
    /// The children are usually built before the parent node. However, if a builder
    /// associated to a node is an instance of <see cref="QueryTreeBuilder{TQuery}"/>, the node is
    /// delegated to this builder and it's responsible to build the node and its
    /// children.
    /// </para>
    /// <seealso cref="IQueryBuilder{TQuery}"/>
    /// </summary>
    public class QueryTreeBuilder<TQuery> : QueryTreeBuilder, IQueryBuilder<TQuery>
    {
        private IDictionary<Type, IQueryBuilder<TQuery>> queryNodeBuilders;

        private IDictionary<string, IQueryBuilder<TQuery>> fieldNameBuilders;

        /// <summary>
        /// <see cref="QueryTreeBuilder{TQuery}"/> constructor.
        /// </summary>
        public QueryTreeBuilder()
        {
            // empty constructor
        }

        /// <summary>
        /// Associates a field name with a builder.
        /// </summary>
        /// <param name="fieldName">the field name</param>
        /// <param name="builder">the builder to be associated</param>
        public virtual void SetBuilder(string fieldName, IQueryBuilder<TQuery> builder)
        {

            if (this.fieldNameBuilders is null)
            {
                this.fieldNameBuilders = new Dictionary<string, IQueryBuilder<TQuery>>();
            }

            this.fieldNameBuilders[fieldName] = builder;
        }

        /// <summary>
        /// Associates a <see cref="Type">class</see> (that implements <see cref="IQueryNode"/>) with a builder
        /// </summary>
        /// <param name="queryNodeClass">The type (a class that implements <see cref="IQueryNode"/>)</param>
        /// <param name="builder">the builder to be associated</param>
        public virtual void SetBuilder(Type queryNodeClass,
            IQueryBuilder<TQuery> builder)
        {
            if (this.queryNodeBuilders is null)
            {
                this.queryNodeBuilders = new Dictionary<Type, IQueryBuilder<TQuery>>();
            }

            this.queryNodeBuilders[queryNodeClass] = builder;
        }

        private void Process(IQueryNode node)
        {
            if (node is object)
            {
                IQueryBuilder<TQuery> builder = GetBuilder(node);

                if (!(builder is QueryTreeBuilder<TQuery>))
                {
                    IList<IQueryNode> children = node.GetChildren();

                    if (children is object)
                    {

                        foreach (IQueryNode child in children)
                        {
                            Process(child);
                        }
                    }
                }

                ProcessNode(node, builder);
            }
        }

        private IQueryBuilder<TQuery> GetBuilder(IQueryNode node)
        {
            IQueryBuilder<TQuery> builder = null;

            if (this.fieldNameBuilders is object && (!(node is null) && node is IFieldableNode fieldNode))
            {
                string field = fieldNode.Field;
                this.fieldNameBuilders.TryGetValue(field, out builder);
            }

            if (builder is null && this.queryNodeBuilders is object)
            {
                Type clazz = node.GetType();

                do
                {
                    builder = GetQueryBuilder(clazz);

                    if (builder is null)
                    {
                        Type[] classes = clazz.GetInterfaces();

                        foreach (Type actualClass in classes)
                        {
                            builder = GetQueryBuilder(actualClass);

                            if (builder is object)
                            {
                                break;
                            }
                        }
                    }
                } while (builder is null && (clazz = clazz.BaseType) is object);
            }

            return builder;
        }

        private void ProcessNode(IQueryNode node, IQueryBuilder<TQuery> builder)
        {
            if (builder is null)
            {
                throw new QueryNodeException(new Message(
                    QueryParserMessages.LUCENE_QUERY_CONVERSION_ERROR, node
                        .ToQueryString(new EscapeQuerySyntax()), node.GetType()
                        .Name));
            }

            object obj = builder.Build(node);

            if (obj is object)
            {
                node.SetTag(QUERY_TREE_BUILDER_TAGID, obj);
            }

        }

        private IQueryBuilder<TQuery> GetQueryBuilder(Type clazz)
        {
            if (typeof(IQueryNode).IsAssignableFrom(clazz))
            {
                IQueryBuilder<TQuery> result;
                this.queryNodeBuilders.TryGetValue(clazz, out result);
                return result;
            }

            return null;
        }

        /// <summary>
        /// Builds some kind of object from a query tree. Each node in the query tree
        /// is built using an specific builder associated to it.
        /// </summary>
        /// <param name="queryNode">the query tree root node</param>
        /// <returns>the built object</returns>
        /// <exception cref="QueryNodeException">if some node builder throws a 
        /// <see cref="QueryNodeException"/> or if there is a node which had no
        /// builder associated to it</exception>
        public virtual TQuery Build(IQueryNode queryNode)
        {
            Process(queryNode);

            return (TQuery)queryNode.GetTag(QUERY_TREE_BUILDER_TAGID);
        }
    }

    /// <summary>
    /// LUCENENET specific class for accessing static members of <see cref="QueryTreeBuilder{TQuery}"/>
    /// without referencing its generic closing type.
    /// </summary>
    public abstract class QueryTreeBuilder
    {
        /// <summary>
        /// This tag is used to tag the nodes in a query tree with the built objects
        /// produced from their own associated builder.
        /// </summary>
        public static readonly string QUERY_TREE_BUILDER_TAGID = typeof(QueryTreeBuilder).Name;
    }
}
