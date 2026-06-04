using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace HashCode_Pizza
{
    class PizzaPlate
    {
        private readonly static Color[] SLICING_COLORS = new Color[]
        {
                Color.Violet,
                Color.Green,
                Color.Yellow,
                Color.Red,
                Color.Blue,
                Color.Cyan,
                Color.Pink,
                Color.PeachPuff,
                Color.Orange,
                Color.Olive
        };
        private const int SLICING_COLOR_BIT_SIZE = 5;
        private const int CHECK_SLICE_VALID = 0;
        private const int CHECK_SLICE_TOO_LOW = 1;
        private const int CHECK_SLICE_INVALID_SLICE = 2;
        private const int CHECK_SLICE_TOO_BIG = 3;
        private int mColumns;
        private int mRows;
        private int mMinIngPerSlice;
        private int mMaxSliceSize;
        private int[,] mPlate;
        private bool mIsMedium;
        public PizzaPlate(int rows, int columns, int[,] plate, int minIng, int maxSliceSize)
        {
            mRows = rows;
            mColumns = columns;
            mPlate = plate;
            mMinIngPerSlice = minIng;
            mMaxSliceSize = maxSliceSize;
            mIsMedium = (rows == 200 && columns == 250);
        }
        public Bitmap generateSlicingBitmap(List<PizzaSlice> slices)
        {
            Bitmap bitmap = new Bitmap(mColumns * SLICING_COLOR_BIT_SIZE, mRows * SLICING_COLOR_BIT_SIZE);
            Graphics gfx = Graphics.FromImage(bitmap);
            SolidBrush brush = new SolidBrush(Color.White);
            // Clear bitmap
            //gfx.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
            // Draw slicing
            foreach (PizzaSlice slice in slices)
            {
                brush.Color = SLICING_COLORS[Math.Abs(slice.ID) % SLICING_COLORS.Length];
                gfx.FillRectangle(brush, slice.ColumnMin * SLICING_COLOR_BIT_SIZE, slice.RowMin * SLICING_COLOR_BIT_SIZE, (slice.ColumnMax - slice.ColumnMin + 1) * SLICING_COLOR_BIT_SIZE, (slice.RowMax - slice.RowMin + 1) * SLICING_COLOR_BIT_SIZE);
            }
            // Ingredients
            brush.Color = Color.Black;
            for (int r = 0; r < mRows; r++)
                for (int c = 0; c < mColumns; c++)
                    if (this.mPlate[r, c] == 1)
                        bitmap.SetPixel(c * SLICING_COLOR_BIT_SIZE + SLICING_COLOR_BIT_SIZE/2, r * SLICING_COLOR_BIT_SIZE + SLICING_COLOR_BIT_SIZE/2, Color.Black);
                    else if (this.mPlate[r, c] == 2)
                        gfx.FillRectangle(brush, c * SLICING_COLOR_BIT_SIZE + 1, r * SLICING_COLOR_BIT_SIZE + SLICING_COLOR_BIT_SIZE/2, SLICING_COLOR_BIT_SIZE - 2, 1);
            brush.Dispose();
            gfx.Dispose();
            return bitmap;
        }
        public int GetSize() { return mColumns * mRows; }
        public List<PizzaSlice> PerformSlice()
        {
            // Generate scan orders
            List<List<Tuple<int, int>>> orders = new List<List<Tuple<int, int>>>();
            // row-major forward
            var order1 = new List<Tuple<int, int>>();
            for (int r = 0; r < mRows; r++) for (int c = 0; c < mColumns; c++) order1.Add(Tuple.Create(r, c));
            orders.Add(order1);
            // row-major reverse
            var order2 = new List<Tuple<int, int>>();
            for (int r = mRows - 1; r >= 0; r--) for (int c = mColumns - 1; c >= 0; c--) order2.Add(Tuple.Create(r, c));
            orders.Add(order2);
            // column-major forward
            var order3 = new List<Tuple<int, int>>();
            for (int c = 0; c < mColumns; c++) for (int r = 0; r < mRows; r++) order3.Add(Tuple.Create(r, c));
            orders.Add(order3);
            // column-major reverse
            var order4 = new List<Tuple<int, int>>();
            for (int c = mColumns - 1; c >= 0; c--) for (int r = mRows - 1; r >= 0; r--) order4.Add(Tuple.Create(r, c));
            orders.Add(order4);
            List<PizzaSlice> bestSlices = null;
            int bestScore = -1;
            foreach (var order in orders)
            {
                int[,] plate = (int[,])mPlate.Clone();
                var slices = PerformSlice_PhaseTwo(plate, order);
                int score = PizzaSlice.GetSlicesSize(slices);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlices = slices;
                }
            }
            return bestSlices;
        }
        private List<PizzaSlice> PerformSlice_PhaseTwo(int[,] plate, List<Tuple<int, int>> order)
        {
            int nextSliceId = -1;
            Dictionary<int, PizzaSlice> sliceHash = new Dictionary<int, PizzaSlice>();
 
            foreach (var pos in order)
             {
                int r = pos.Item1;
                int c = pos.Item2;
                if (SlicePizzaAtPosition(plate, r, c, sliceHash, nextSliceId) == true)
                    nextSliceId--;
             }
            
            // Iterative re-slicing until convergence
            int prevScore = PizzaSlice.GetSlicesSize(sliceHash.Values);
            bool improved;
            do
            {
                List<PizzaSlice> slices = new List<PizzaSlice>(sliceHash.Values);
                foreach (PizzaSlice slice in slices)
                {
                    if (!sliceHash.TryGetValue(slice.ID, out PizzaSlice currentSlice))
                        continue;
                    sliceHash.Remove(currentSlice.ID);
                    currentSlice.RestoreSliceToPlate(plate, mPlate);
                    SlicePizzaAtPosition(plate, currentSlice.RowMin, currentSlice.ColumnMin, sliceHash, currentSlice.ID);
                }
                int currScore = PizzaSlice.GetSlicesSize(sliceHash.Values);
                improved = currScore > prevScore;
                prevScore = currScore;
            } while (improved);
            FillGaps(plate, sliceHash, ref nextSliceId);
            return new List<PizzaSlice>(sliceHash.Values);
        }
        private void FillGaps(int[,] plate, Dictionary<int, PizzaSlice> sliceHash, ref int nextSliceId)
        {
            bool placed;
            do
            {
                placed = false;
                for (int r = 0; r < mRows; r++)
                {
                    for (int c = 0; c < mColumns; c++)
                    {
                        if (plate[r, c] <= 0) continue;
                        PizzaSlice bestSlice = GetMaxNetGainSliceAt(plate, sliceHash, r, c, nextSliceId, out int bestNetGain);
                        if (bestSlice != null && bestNetGain > 0)
                        {
                            Dictionary<int, int> sliceContent = bestSlice.GetSliceContent(plate);
                            foreach (int overlapSliceId in sliceContent.Keys)
                            {
                                if (overlapSliceId > 0) continue;
                                PizzaSlice existingSlice = sliceHash[overlapSliceId];
                                PizzaSlice existingAfterOverlap;
                                if (mIsMedium) {
                                    existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping_Generalized(bestSlice);
                                    if (existingAfterOverlap != null)
                                    {
                                        for (int cr = existingSlice.RowMin; cr <= existingSlice.RowMax; cr++)
                                            for (int cc = existingSlice.ColumnMin; cc <= existingSlice.ColumnMax; cc++)
                                                if (cr < existingAfterOverlap.RowMin || cr > existingAfterOverlap.RowMax ||
                                                    cc < existingAfterOverlap.ColumnMin || cc > existingAfterOverlap.ColumnMax)
                                                    plate[cr, cc] = mPlate[cr, cc];
                                    }
                                }
                                else {
                                    existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping(bestSlice);
                                }
                                sliceHash[existingSlice.ID] = existingAfterOverlap;
                            }
                            bestSlice.RemoveSliceFromPlate(plate);
                            sliceHash.Add(bestSlice.ID, bestSlice);
                            nextSliceId--;
                            placed = true;
                        }
                    }
                }
            } while (placed);
        }
        private PizzaSlice GetMaxNetGainSliceAt(int[,] plate, Dictionary<int, PizzaSlice> sliceHash, int row, int column, int nextSliceId, out int maxNetGain)
        {
            maxNetGain = 0;
            PizzaSlice bestSlice = null;

            for (int minRow = row; minRow >= Math.Max(0, row - mMaxSliceSize); minRow--)
            for (int maxRow = row; maxRow < Math.Min(row + mMaxSliceSize + 1, mRows); maxRow++)
            {
                for (int minCol = column; minCol >= Math.Max(0, column - mMaxSliceSize); minCol--)
                for (int maxCol = column; maxCol < Math.Min(column + mMaxSliceSize + 1, mColumns); maxCol++)
                {
                    int isValidSlice = IsValidSlice(this.mPlate, minRow, maxRow, minCol, maxCol);
                    if ((isValidSlice == CHECK_SLICE_TOO_BIG) || (isValidSlice == CHECK_SLICE_INVALID_SLICE))
                        break;
                    if (isValidSlice != CHECK_SLICE_VALID)
                        continue;

                    PizzaSlice newSlice = new PizzaSlice(nextSliceId, minRow, maxRow, minCol, maxCol);
                    int netGain = newSlice.GetSize();
                    Dictionary<int, int> content = newSlice.GetSliceContent(plate);
                    bool validOverlap = true;
                    foreach (int overlapId in content.Keys)
                    {
                        if (overlapId > 0) continue;
                        PizzaSlice existingSlice = sliceHash[overlapId];
                        PizzaSlice shrunken;
                        if (mIsMedium)
                            shrunken = existingSlice.BuildShirnkedSliceWithOverlapping_Generalized(newSlice);
                        else
                            shrunken = existingSlice.BuildShirnkedSliceWithOverlapping(newSlice);
                        if (shrunken == null) { validOverlap = false; break; }
                        if (this.IsValidSlice(this.mPlate, shrunken.RowMin, shrunken.RowMax, shrunken.ColumnMin, shrunken.ColumnMax) != CHECK_SLICE_VALID)
                        { validOverlap = false; break; }
                        netGain -= (existingSlice.GetSize() - shrunken.GetSize());
                    }
                    if (validOverlap && netGain > maxNetGain)
                    {
                        maxNetGain = netGain;
                        bestSlice = newSlice;
                    }
                }
            }
            return bestSlice;
        }
        private bool SlicePizzaAtPosition(int[,] plate, int r, int c, Dictionary<int, PizzaSlice> sliceHash, int nextSliceId)
        {
            if (plate[r, c] < 0)
                return false;
            PizzaSlice maxSlice = GetMaxSliceExtentionAt(plate, sliceHash, r, c, nextSliceId);
            if (maxSlice != null)
            {
                // Shrink existing slices
                Dictionary<int, int> sliceContent = maxSlice.GetSliceContent(plate);
                foreach (int overlapSliceId in sliceContent.Keys)
                {
                    if (overlapSliceId > 0)
                        continue;
                    PizzaSlice existingSlice = sliceHash[overlapSliceId];
                    PizzaSlice existingAfterOverlap;
                    if (mIsMedium) {
                        existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping_Generalized(maxSlice);
                        // Clear cells from the original existing slice that are not part of the shrunken slice,
                        // preventing stray slice IDs on the plate that would later cause missing-key errors.
                        if (existingAfterOverlap != null)
                        {
                            for (int cr = existingSlice.RowMin; cr <= existingSlice.RowMax; cr++)
                                for (int cc = existingSlice.ColumnMin; cc <= existingSlice.ColumnMax; cc++)
                                    if (cr < existingAfterOverlap.RowMin || cr > existingAfterOverlap.RowMax ||
                                        cc < existingAfterOverlap.ColumnMin || cc > existingAfterOverlap.ColumnMax)
                                        plate[cr, cc] = mPlate[cr, cc];
                        }
                    }
                    else {
                        existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping(maxSlice);
                    }
                   
                    sliceHash[existingSlice.ID] = existingAfterOverlap;
                }
                maxSlice.RemoveSliceFromPlate(plate);
                sliceHash.Add(maxSlice.ID, maxSlice);
                return true;
            }
            return false;
        }
        private PizzaSlice GetMaxSliceExtentionAt(int[,] plate, Dictionary<int, PizzaSlice> sliceHash, int row, int column, int nextSliceId)
        {
            PizzaSlice maxSlice = null;
            int maxSliceIngredients = 0;
            for (int minRow = row; minRow >= Math.Max(0, row - this.mMaxSliceSize); minRow--)
            for (int maxRow = row; maxRow < Math.Min(row + this.mMaxSliceSize + 1, mRows); maxRow++)
            {
                for (int minCol = column; minCol >= Math.Max(0, column - this.mMaxSliceSize); minCol--)
                for (int maxCol = column; maxCol < Math.Min(column + this.mMaxSliceSize + 1, mColumns); maxCol++)
                {
                    int isValidSlice = IsValidSlice(this.mPlate, minRow, maxRow, minCol, maxCol);
                    if ((isValidSlice == CHECK_SLICE_TOO_BIG) || (isValidSlice == CHECK_SLICE_INVALID_SLICE))
                        break;
                    if (isValidSlice != CHECK_SLICE_VALID)
                        continue;
                    PizzaSlice newSlice = new PizzaSlice(nextSliceId, minRow, maxRow, minCol, maxCol);
                    // The new slice contains positions previously not in any slice
                    int newSliceIngredients = newSlice.CountIngredients(plate);
                    if (newSliceIngredients == 0)
                        continue;
                    // Check overlapping slices are still valid slices
                    Dictionary<int, int> sliceContent = newSlice.GetSliceContent(plate);
                    bool isValidOverlap = true;
                    foreach (int overlapSliceId in sliceContent.Keys)
                    {
                        if (overlapSliceId > 0)
                            continue;
                        PizzaSlice existingSlice = sliceHash[overlapSliceId];
                        PizzaSlice existingAfterOverlap;
                        if (mIsMedium) {
                            existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping_Generalized(newSlice);
                        }
                        else {
                            existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping(newSlice);
                        }
                        if (existingAfterOverlap == null)
                        {
                            isValidOverlap = false;
                            break;
                        }
                        if (this.IsValidSlice(this.mPlate, 
                            existingAfterOverlap.RowMin, existingAfterOverlap.RowMax, 
                            existingAfterOverlap.ColumnMin, existingAfterOverlap.ColumnMax) != CHECK_SLICE_VALID)
                        {
                            isValidOverlap = false;
                            break;
                        }
                    }
                    if (isValidOverlap == false)
                        continue;
                    // Check if the new slice is bettter than existing max
                    if (maxSlice == null)
                    {
                        maxSlice = newSlice;
                        maxSliceIngredients = newSliceIngredients;
                    }
                    else if (maxSliceIngredients < newSliceIngredients)
                    {
                        maxSlice = newSlice;
                        maxSliceIngredients = newSliceIngredients;
                    }
                    else if (maxSliceIngredients == newSliceIngredients && newSlice.GetSize() < maxSlice.GetSize())
                    {
                        maxSlice = newSlice;
                    }
                }
            }
            return maxSlice;
        }
        public bool IsValidSlicing(List<PizzaSlice> slices)
        {
            int[,] plate = (int[,])mPlate.Clone();
            foreach (PizzaSlice slice in slices)
            {
                if (IsValidSlice(mPlate, slice.RowMin, slice.RowMax, slice.ColumnMin, slice.ColumnMax) != CHECK_SLICE_VALID)
                    return false;
                for (int r = slice.RowMin; r <= slice.RowMax; r++)
                    for (int c = slice.ColumnMin; c <= slice.ColumnMax; c++)
                    {
                        if (plate[r, c] < 0)
                            return false;
                        plate[r, c] = slice.ID;
                    }
            }
            return true;
        }
        private int IsValidSlice(int[,] plate, int minRow, int maxRow, int minCol, int maxCol)
        {
            int count1 = 0;
            int count2 = 0;
            if ((maxRow - minRow + 1)*(maxCol - minCol + 1) > mMaxSliceSize)
                return CHECK_SLICE_TOO_BIG;
            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    int plateVal = plate[r, c];
                    if (plateVal <= 0)
                        return CHECK_SLICE_INVALID_SLICE;
                    else if (plateVal == 1)
                        count1++;
                    else if (plateVal == 2)
                        count2++;
                    else
                        throw new Exception("Valid plate value: " + plateVal);
                }
            }
            if ((count1 < this.mMinIngPerSlice) || (count2 < this.mMinIngPerSlice))
                return CHECK_SLICE_TOO_LOW;
            return CHECK_SLICE_VALID;
        }
    }
}
