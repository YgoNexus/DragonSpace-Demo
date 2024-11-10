namespace DragonSpace.Grids
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using DragonSpace.Structs;
    public class LooseDoubleGrid
    {
        FreeLinkedList<LooseCell>[][] coarseGrid;
        //private CoarseCell[][] coarseGrid;

        private readonly LooseCell[][] grid;

        /// <summary>
        /// the size of the grid
        /// </summary>
        private readonly int boundWidth, boundHeight;

        // Stores the number of columns and rows in the grid.
        private readonly int _numRows, _numCols;

        private readonly int _coarseRows, _coarseCols;

        /// <summary>
        /// stores the size of a cell
        /// </summary>
        private readonly float _invCellWidth, _invCellHeight;

        /// <summary>
        /// The size of all elements stored in the grid.
        /// </summary>
        private readonly float _invCoarseWidth, _invCoarseHeight;

        public LooseDoubleGrid(float cellWidth, float cellHeight, float coarseWidth, float coarseHeight,
            int boundWidth, int boundHeight)
        {
            if (cellWidth > coarseWidth || cellHeight > coarseHeight)
            {
                throw new ArgumentException("Coarse cells must be (significantly) larger than cell size");
            }
            this.boundWidth = boundWidth;
            this.boundHeight = boundHeight;
            _numRows = (int)( boundHeight / cellHeight ) + 1;
            _numCols = (int)( boundWidth / cellWidth ) + 1;
            _invCellWidth = 1 / cellWidth;
            _invCellHeight = 1 / cellHeight;
            _coarseRows = (int)( boundHeight / coarseHeight ) + 1;
            _coarseCols = (int)( boundWidth / coarseWidth ) + 1;
            _invCoarseWidth = 1 / coarseWidth;
            _invCoarseHeight = 1 / coarseHeight;

            //init rows
            grid = new LooseCell[_numRows][];
            //init columns
            for (int i = 0; i < _numRows; i++)
            {
                grid[i] = new LooseCell[_numCols];
                for (int j = 0; j < grid[i].Length; j++)
                {
                    grid[i][j] = new LooseCell();
                }
            }

            //coarseGrid = new CoarseGrid(_coarseCols, _coarseRows);

            coarseGrid = new FreeLinkedList<LooseCell>[_coarseRows][];
            for (int y = 0; y < _coarseRows; y++)
            {
                coarseGrid[y] = new FreeLinkedList<LooseCell>[_coarseCols];
                for (int x = 0; x < _coarseCols; x++)
                {
                    coarseGrid[y][x] = new(_coarseCols * _coarseRows);
                }
            }
        }

        #region Public methods
        /// <summary>
        /// Inserts an object into the grid, expanding the cell to fit the element
        /// </summary>
        /// <param name="obj">The object to insert</param>
        public void Insert(IGridElt obj)
        {
            int xIdx = GridLocalToCellCol(obj.LeftX);
            int yIdx = GridLocalToCellRow(obj.BottomY);
            InsertToCell(obj, xIdx, yIdx);
        }

        /// <summary>
        /// Inserts an object into a grid cell at the given index
        /// </summary>
        /// <param name="elt">The object to insert</param>
        /// <param name="xIdx">The column index of the cell</param>
        /// <param name="yIdx">The row index of the cell</param>
        private void InsertToCell(IGridElt elt, int xIdx, int yIdx)
        {
            //if the cell is empty, initialize the bounds to match the element
            if (grid[yIdx][xIdx].FirstElt == null)
            {
                grid[yIdx][xIdx].Push(elt);
                grid[yIdx][xIdx].lft = (int)elt.LeftX;
                grid[yIdx][xIdx].btm = (int)elt.BottomY;
                grid[yIdx][xIdx].rgt = grid[yIdx][xIdx].lft + elt.Width;
                grid[yIdx][xIdx].top = grid[yIdx][xIdx].btm + elt.Height;

                //insert into the tight cells it overlaps
                InsertToCoarseGrid(yIdx, xIdx);
            }
            else    //otherwise, see if the bounds need to change to fit the element
            {
                grid[yIdx][xIdx].Push(elt);
                ExpandCell(yIdx, xIdx, elt);
            }
        }
        /// <summary>
        /// Removes an element from the grid. The <see cref="IUGridElt"/> of the object must give
        /// the same position where the element is currently in the grid
        /// </summary>
        /// <param name="obj">The object to remove</param>
        public void Remove(IGridElt obj)
        {
            int xIdx = GridLocalToCellCol(obj.LeftX);
            int yIdx = GridLocalToCellRow(obj.BottomY);
            RemoveFromCell(obj, xIdx, yIdx);
        }
        private void RemoveFromCell(IGridElt obj, int xIdx, int yIdx)
        {
            IGridElt elt = grid[yIdx][xIdx].FirstElt;
            IGridElt prevElt = null;

            while (elt != null && elt.ID != obj.ID)
            {
                prevElt = elt;
                elt = elt.NextElt;
            }

            if (prevElt == null)
                grid[yIdx][xIdx].Pop();
            else
                prevElt.NextElt = elt.NextElt;
        }
        /// <summary>
        /// Moves an element in the grid from the former position to the new one.
        /// </summary>
        /// <param name="obj">The object to move</param>
        /// <param name="fromX">The current position of the object in the grid</param>
        /// <param name="fromY">The current position of the object in the grid</param>
        /// <param name="toX">The new position of the object in the grid</param>
        /// <param name="toY">The new position of the object in the grid</param>
        public void Move(IGridElt obj, float fromX, float fromY, float toX, float toY)
        {
            int oldCol = GridLocalToCellCol(fromX);
            int oldRow = GridLocalToCellRow(fromY);
            int newCol = GridLocalToCellCol(toX);
            int newRow = GridLocalToCellRow(toY);

            //ref LooseGridRow row = ref looseGridRowArr[oldRow];

            if (oldCol != newCol || oldRow != newRow)
            {
                RemoveFromCell(obj, oldCol, oldRow);
                InsertToCell(obj, newCol, newRow);
            }
            else
            {
                //just expand the cell if necessary, we can contract it later
                ExpandCell(oldRow, oldCol, obj);
            }
        }

        private readonly List<IGridElt> _queryResults = new List<IGridElt>(16);
        /// <summary>
        /// Returns all the elements that intersect the specified rectangle excluding 
        /// elements with the specified ID to omit.
        /// </summary>
        /// <param name="omitEltID">The ID of the element from the <see cref="IGridElt"/> interface to omit from results</param>
        /// <returns>A <see cref="List{T}"/> of <see cref="IGridElt"/>s</returns>
        public List<IGridElt> Query(float lft, float btm, float rgt, float top, int omitEltID = int.MinValue)
        {
            // Find the coarse cells that intersect the search query.
            int minX = GridLocalToCoarseCol(lft);
            int minY = GridLocalToCoarseRow(btm);
            int maxX = GridLocalToCoarseCol(rgt);
            int maxY = GridLocalToCoarseRow(top);

            AABB query = new AABB(lft, top, rgt, btm);

            _queryResults.Clear();

            for (int y = minY; y <= maxY; ++y)
            {
                for (int x = minX; x <= maxX; ++x)
                {
                    if (coarseGrid[y][x].Count == 0)
                        continue;

                    //go through the linked list of loose cells
                    FreeLinkedList<LooseCell>.FreeElement cNode = coarseGrid[y][x].FirstItem;
                    for (int i = coarseGrid[y][x].Count - 1; i >= 0; --i)
                    {
                        if (RectOverlap(in query, in cNode.element))
                        {
                            // check elements in cell
                            IGridElt elt = cNode.element.FirstElt;
                            while (elt != null)
                            {
                                if (RectOverlap(in query, in elt) && elt.ID != omitEltID)
                                    _queryResults.Add(elt);
                                elt = elt.NextElt;
                            }
                        }
                        cNode = coarseGrid[y][x][cNode.next];
                    }
                }
            }
            return _queryResults;
        }

        /// <summary>
        /// Contracts all the loose cell boundaries and removes them from coarse cells.
        /// Run at the end of every frame or update
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TightenUp()
        {
            //remove all loose cells from the coarse grid
            for (int y = 0; y < _coarseRows; ++y)
            {
                for (int x = 0; x < _coarseCols; ++x)
                {
                    coarseGrid[y][x].Clear();
                }
            }

            //contract all loose cells, then add back to the coarse grid
            for (int i = grid.Length - 1; i >= 0; i--)
            {
                for (int j = grid[i].Length - 1; j >= 0; j--)
                {
                    ContractCell(i, j);
                    InsertToCoarseGrid(i, j);
                }
            }
        }

        /// <summary>
        /// Calls <see cref="ILooseGridVisitor.CoarseGrid(float, float, float, float)"/> with the grid data
        /// Then traverses the whole grid and calls <see cref="IUniformGridVisitor.Cell(int, int)"/>
        /// on any non-empty cells.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Traverse(ILooseGridVisitor visitor)
        {
            visitor.CoarseGrid(boundWidth, boundHeight, 1f / _invCoarseWidth, 1f / _invCoarseHeight);

            visitor.LooseGrid(boundWidth, boundHeight, 1f / _invCellWidth, 1f / _invCellHeight);

            //go through the grid
            for (int y = 0; y < _coarseRows; ++y)
            {
                for (int x = 0; x < _coarseCols; ++x)
                {
                    visitor.CoarseCell(/*grid.cells[y][x].looseCells.Count*/   233,
                        x, y, 1f / _invCoarseWidth, 1f / _invCoarseHeight);
                }
            }

            for (int i = grid.Length - 1; i >= 0; i--)
            {
                for (int j = grid[i].Length - 1; j >= 0; j--)
                {
                    LooseCell cell = grid[i][j];
                    visitor.LooseCell(cell.FirstElt, new AABB(cell.lft, cell.top, cell.rgt, cell.btm));
                }
            }
        }
        #endregion

        #region Private methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertToCoarseGrid(int row, int col)
        {
            int minX = GridLocalToCoarseCol(grid[row][col].lft);
            int minY = GridLocalToCoarseRow(grid[row][col].btm);
            int maxX = GridLocalToCoarseCol(grid[row][col].rgt);
            int maxY = GridLocalToCoarseRow(grid[row][col].top);

            for (int y = minY; y <= maxY; ++y)
            {
                for (int x = minX; x <= maxX; ++x)
                {
                    coarseGrid[y][x].InsertFirst(grid[row][col]);
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandCell(int row, int col, IGridElt elt)
        {
            int xMin1 = GridLocalToCoarseCol(grid[row][col].lft);
            int yMin1 = GridLocalToCoarseRow(grid[row][col].btm);
            int xMax1 = GridLocalToCoarseCol(grid[row][col].rgt);
            int yMax1 = GridLocalToCoarseRow(grid[row][col].top);

            int eLft = (int)elt.LeftX;
            int eBtm = (int)elt.BottomY;

            grid[row][col].lft = Math.Min(grid[row][col].lft, eLft);
            grid[row][col].btm = Math.Min(grid[row][col].btm, eBtm);
            grid[row][col].rgt = Math.Max(grid[row][col].rgt, eLft + elt.Width);
            grid[row][col].top = Math.Max(grid[row][col].top, eBtm + elt.Height);

            int xMax2 = GridLocalToCoarseCol(grid[row][col].rgt);
            int yMax2 = GridLocalToCoarseRow(grid[row][col].top);

            //insert into new coarse cells
            int xdiff = ( xMax2 > xMax1 ) ? 1 : 0;
            if (xMax1 != xMax2 || yMax1 != yMax2)
            {
                for (int y = yMin1; y <= yMax2; ++y)
                {
                    //if in an old row, only do the new columns,
                    //otherwise do the whole row
                    int x = ( y > yMax1 ) ? xMin1 : xMax1 + xdiff;
                    for (; x <= xMax2; ++x)
                    {
                        coarseGrid[y][x].InsertFirst(grid[row][col]);
                    }
                }
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ContractCell(int row, int col)
        {
            grid[row][col].lft = grid[row][col].btm = int.MaxValue;
            grid[row][col].rgt = grid[row][col].top = int.MinValue;
            IGridElt elt = grid[row][col].FirstElt;
            if (elt != null)
            {
                grid[row][col].lft = (int)elt.LeftX;
                grid[row][col].btm = (int)elt.BottomY;
                grid[row][col].rgt = grid[row][col].lft + elt.Width;
                grid[row][col].top = grid[row][col].btm + elt.Height;

                elt = elt.NextElt;
                while (elt != null)
                {
                    int eLft = (int)elt.LeftX;
                    int eBtm = (int)elt.BottomY;

                    grid[row][col].lft = Math.Min(grid[row][col].lft, eLft);
                    grid[row][col].btm = Math.Min(grid[row][col].btm, eBtm);
                    grid[row][col].rgt = Math.Max(grid[row][col].rgt, eLft + elt.Width);
                    grid[row][col].top = Math.Max(grid[row][col].top, eBtm + elt.Height);

                    elt = elt.NextElt;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GridLocalToCoarseRow(float y)
        {
            if (y <= 0)
            { return 0; }
            return Math.Min((int)( y * _invCoarseHeight ), _coarseRows - 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GridLocalToCoarseCol(float x)
        {
            if (x <= 0)
            { return 0; }
            return Math.Min((int)( x * _invCoarseWidth ), _coarseCols - 1);
        }

        // Returns the grid cell Y index for the specified position.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GridLocalToCellRow(float y)
        {
            if (y <= 0)
            { return 0; }
            return Math.Min((int)( y * _invCellHeight ), _numRows - 1);
        }

        // Returns the grid cell X index for the specified position.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GridLocalToCellCol(float x)
        {
            if (x <= 0)
            { return 0; }
            return Math.Min((int)( x * _invCellWidth ), _numCols - 1);
        }

        //TODO: move somewhere more useful
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(in AABB a, in IGridElt b)
        {
            int bLft = (int)b.LeftX;
            int bBtm = (int)b.BottomY;
            return RectOverlap(a.lft, a.top, a.rgt, a.btm,
                bLft, bBtm + b.Height, bLft + b.Width, bBtm);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(in AABB a, in LooseCell b)
        {
            return RectOverlap(a.lft, a.top, a.rgt, a.btm,
                b.lft, b.top, b.rgt, b.btm);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(int l1, int t1, int r1, int b1,
                                      int l2, int t2, int r2, int b2)
        {
            return l2 <= r1 && r2 >= l1 && t2 >= b1 && b2 <= t1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RectOverlap(float l1, float t1, float r1, float b1,
                                      float l2, float t2, float r2, float b2)
        {
            return l2 <= r1 && r2 >= l1 && t2 >= b1 && b2 <= t1;
        }
        #endregion

        //=============================================
        #region child classes

        //private class CoarseGrid
        //{
        //    public readonly CoarseCell[][] coarseCellArr;

        //    public CoarseGrid(int cellsWide, int cellsHigh)
        //    {
        //        int cellsPerCell = cellsWide * cellsHigh;
        //        cellsPerCell = 4;
        //        coarseCellArr = new CoarseCell[cellsHigh][];
        //        for (int y = cellsHigh - 1; y >= 0; --y)
        //        {
        //            coarseCellArr[y] = new CoarseCell[cellsWide];
        //            for (int x = cellsWide - 1; x >= 0; --x)
        //            {
        //                coarseCellArr[y][x] = new CoarseCell(cellsPerCell);
        //            }
        //        }
        //    }
        //}

        //private class CoarseCell
        //{
        //    /// <summary>
        //    /// A singly linked list of all the loose cells in this tight cell
        //    /// </summary>
        //    public readonly FreeLinkedList<LooseCell> looseCells;

        //    /// <param name="cap">How much space to allocate. Should be the number of loose cells that the 
        //    /// coarse cell spans, or slightly more.</param>
        //    public CoarseCell(int cap = 4)
        //    {
        //        looseCells = new FreeLinkedList<LooseCell>(cap);
        //    }
        //}

        //TODO: This would be faster as a struct b/c data locality
        //      but doesn't work with the way I'm inserting them into coarse cells
        //      not worth addressing now, this way works and that's good enough.
        //      maybe try out the one big list method since that would reduce copies?
        //      then query overlapping cells and put them in a stack 
        //      before checking for overlapping elements
        private struct LooseCell
        {
            /// <summary>
            /// The first element in the linked list for this cell, 
            /// the rest are accessed by <see cref="IGridElt.NextElt"/>
            /// </summary>
            public IGridElt FirstElt;

            /// <summary>X coordinate of the cell's bottom-left corner</summary>
            public int lft;
            /// <summary>Y coordinate of the cell's bottom-left corner</summary>
            public int btm;
            /// <summary>Width of the cell's bounding box</summary>
            public int rgt;
            /// <summary>Height of the cell's bounding box</summary>
            public int top;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(IGridElt elt)
            {
                elt.NextElt = FirstElt;
                FirstElt = elt;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Pop()
            {
                FirstElt = FirstElt.NextElt;
            }
        }

        //private struct LooseGridRow
        //{
        //    // Stores all the loose cells in the row. 
        //    // Each cell stores the first element in that cell, 
        //    // which points to the next in the elts list.
        //    public LooseCell[] looseCellArr;

        //    public LooseGridRow(int cellsWide)
        //    {
        //        looseCellArr = new LooseCell[cellsWide];
        //    }
        //}
    }
    #endregion
}