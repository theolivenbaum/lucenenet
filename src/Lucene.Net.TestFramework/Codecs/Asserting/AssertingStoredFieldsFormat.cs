using Lucene.Net.Codecs.Lucene41;
using Lucene.Net.Diagnostics;
using Lucene.Net.Index;
using Lucene.Net.Store;

namespace Lucene.Net.Codecs.Asserting
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
    /// Just like <see cref="Lucene41StoredFieldsFormat"/> but with additional asserts.
    /// </summary>
    public class AssertingStoredFieldsFormat : StoredFieldsFormat
    {
        private readonly StoredFieldsFormat @in = new Lucene41StoredFieldsFormat();

        public override StoredFieldsReader FieldsReader(Directory directory, SegmentInfo si, FieldInfos fn, IOContext context)
        {
            return new AssertingStoredFieldsReader(@in.FieldsReader(directory, si, fn, context), si.DocCount);
        }

        public override StoredFieldsWriter FieldsWriter(Directory directory, SegmentInfo si, IOContext context)
        {
            return new AssertingStoredFieldsWriter(@in.FieldsWriter(directory, si, context));
        }

        internal class AssertingStoredFieldsReader : StoredFieldsReader
        {
            private readonly StoredFieldsReader @in;
            private readonly int maxDoc;

            internal AssertingStoredFieldsReader(StoredFieldsReader @in, int maxDoc)
            {
                this.@in = @in;
                this.maxDoc = maxDoc;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    @in.Dispose();
            }

            public override void VisitDocument(int n, StoredFieldVisitor visitor)
            {
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(n >= 0 && n < maxDoc)) Debugging.ThrowAssert();
                @in.VisitDocument(n, visitor);
            }

            public override object Clone()
            {
                return new AssertingStoredFieldsReader((StoredFieldsReader)@in.Clone(), maxDoc);
            }

            public override long RamBytesUsed()
            {
                return @in.RamBytesUsed();
            }

            public override void CheckIntegrity()
            {
                @in.CheckIntegrity();
            }
        }

        internal enum Status
        {
            UNDEFINED,
            STARTED,
            FINISHED
        }

        internal class AssertingStoredFieldsWriter : StoredFieldsWriter
        {
            private readonly StoredFieldsWriter @in;
            private int numWritten;
            private int fieldCount;
            private Status docStatus;

            internal AssertingStoredFieldsWriter(StoredFieldsWriter @in)
            {
                this.@in = @in;
                this.docStatus = Status.UNDEFINED;
            }

            public override void StartDocument(int numStoredFields)
            {
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(docStatus != Status.STARTED)) Debugging.ThrowAssert();
                @in.StartDocument(numStoredFields);
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(fieldCount == 0)) Debugging.ThrowAssert();
                fieldCount = numStoredFields;
                numWritten++;
                docStatus = Status.STARTED;
            }

            public override void FinishDocument()
            {
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(docStatus == Status.STARTED)) Debugging.ThrowAssert();
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(fieldCount == 0)) Debugging.ThrowAssert();
                @in.FinishDocument();
                docStatus = Status.FINISHED;
            }

            public override void WriteField(FieldInfo info, IIndexableField field)
            {
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(docStatus == Status.STARTED)) Debugging.ThrowAssert();
                @in.WriteField(info, field);
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(fieldCount > 0)) Debugging.ThrowAssert();
                fieldCount--;
            }

            public override void Abort()
            {
                @in.Abort();
            }

            public override void Finish(FieldInfos fis, int numDocs)
            {
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(docStatus == (numDocs > 0 ? Status.FINISHED : Status.UNDEFINED))) Debugging.ThrowAssert();
                @in.Finish(fis, numDocs);
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(fieldCount == 0)) Debugging.ThrowAssert();
                if (Debugging.AssertsEnabled && Debugging.ShouldAssert(numDocs == numWritten)) Debugging.ThrowAssert();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    @in.Dispose();
            }
        }
    }
}