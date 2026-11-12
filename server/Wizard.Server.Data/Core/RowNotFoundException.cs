using System;
using System.Globalization;
using Wizard.Core;

namespace Wizard.Server.Data
{
    // IRowLocks 가 잠글 행을 찾지 못했다(CONTRACTS 7.2의 LockWandAsync 는 null 을 돌려주지 않는다). Code 는 NotFound(HTTP 404)
    public sealed class RowNotFoundException : Exception
    {
        public RowNotFoundException(string table, long id)
            : base(table + " row " + id.ToString(CultureInfo.InvariantCulture) + " not found")
        {
            Table = table;
            Id = id;
        }

        public string Table { get; }

        public long Id { get; }

        public ErrorCode Code => ErrorCode.NotFound;
    }
}
