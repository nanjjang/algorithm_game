using System;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Assets
{
    // 칸 크기가 고른 시트(지팡이 hand·inspection·icon, spec 14 5.4절 SheetRef). 칸 번호는 행 우선(row * Columns + column)
    public sealed class SheetGrid
    {
        public SheetGrid(string file, PixelSize cell, int columns, int rows)
        {
            File = file;
            Cell = cell;
            Columns = columns;
            Rows = rows;
        }

        public string File { get; }

        public PixelSize Cell { get; }

        public int Columns { get; }

        public int Rows { get; }

        public int CellCount => Columns * Rows;

        // 시트 전체 크기(TV-198~200의 PNG 크기와 대조한다)
        public PixelSize SheetSize => new PixelSize(Cell.Width * Columns, Cell.Height * Rows);

        public PixelRect CellRect(int column, int row)
        {
            if (column < 0 || column >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }
            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            return new PixelRect(column * Cell.Width, row * Cell.Height, Cell.Width, Cell.Height);
        }

        public PixelRect CellRect(int index) => CellRect(index % Columns, index / Columns);
    }
}
