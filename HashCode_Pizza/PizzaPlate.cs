﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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
        private bool mIsBig;

        public PizzaPlate(int rows, int columns, int[,] plate, int minIng, int maxSliceSize)
        {
            mRows = rows;
            mColumns = columns;
            mPlate = plate;
            mMinIngPerSlice = minIng;
            mMaxSliceSize = maxSliceSize;
            mIsMedium = (rows == 200 && columns == 250);
            mIsBig = (rows == 1000 && columns == 1000);
        }

        public Bitmap generateSlicingBitmap(List<PizzaSlice> slices)
        {
            Bitmap bitmap = new Bitmap(mColumns * SLICING_COLOR_BIT_SIZE, mRows * SLICING_COLOR_BIT_SIZE);
            Graphics gfx = Graphics.FromImage(bitmap);
            SolidBrush brush = new SolidBrush(Color.White);

            foreach (PizzaSlice slice in slices)
            {
                brush.Color = SLICING_COLORS[Math.Abs(slice.ID) % SLICING_COLORS.Length];
                gfx.FillRectangle(brush, slice.ColumnMin * SLICING_COLOR_BIT_SIZE, slice.RowMin * SLICING_COLOR_BIT_SIZE, (slice.ColumnMax - slice.ColumnMin + 1) * SLICING_COLOR_BIT_SIZE, (slice.RowMax - slice.RowMin + 1) * SLICING_COLOR_BIT_SIZE);
            }

            brush.Color = Color.Black;
            for (int r = 0; r < mRows; r++)
                for (int c = 0; c < mColumns; c++)
                    if (this.mPlate[r, c] == 1)
                        bitmap.SetPixel(c * SLICING_COLOR_BIT_SIZE + SLICING_COLOR_BIT_SIZE / 2, r * SLICING_COLOR_BIT_SIZE + SLICING_COLOR_BIT_SIZE / 2, Color.Black);
                    else if (this.mPlate[r, c] == 2)
                        gfx.FillRectangle(brush, c * SLICING_COLOR_BIT_SIZE + 1, r * SLICING_COLOR_BIT_SIZE + SLICING_COLOR_BIT_SIZE / 2, SLICING_COLOR_BIT_SIZE - 2, 1);

            brush.Dispose();
            gfx.Dispose();
            return bitmap;
        }

        public int GetSize() { return mColumns * mRows; }

        public List<PizzaSlice> PerformSlice()
        {
            List<List<Tuple<int, int>>> orders = new List<List<Tuple<int, int>>>();
            var order1 = new List<Tuple<int, int>>();
            for (int r = 0; r < mRows; r++) for (int c = 0; c < mColumns; c++) order1.Add(Tuple.Create(r, c));
            orders.Add(order1);
            var order2 = new List<Tuple<int, int>>();
            for (int r = mRows - 1; r >= 0; r--) for (int c = mColumns - 1; c >= 0; c--) order2.Add(Tuple.Create(r, c));
            orders.Add(order2);
            var order3 = new List<Tuple<int, int>>();
            for (int c = 0; c < mColumns; c++) for (int r = 0; r < mRows; r++) order3.Add(Tuple.Create(r, c));
            orders.Add(order3);
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
                if (SlicePizzaAtPosition(plate, r, c, sliceHash, nextSliceId))
                    nextSliceId--;
            }

            int prevScore = PizzaSlice.GetSlicesSize(sliceHash.Values);
            bool improved;
            do
            {
                List<PizzaSlice> slices = new List<PizzaSlice>(sliceHash.Values);
                if (mIsBig) slices.Sort((a, b) => b.GetSize().CompareTo(a.GetSize()));
                foreach (PizzaSlice slice in slices)
                {
                    if (!sliceHash.TryGetValue(slice.ID, out PizzaSlice currentSlice))
                        continue;
                    sliceHash.Remove(currentSlice.ID);
                    currentSlice.RestoreSliceToPlate(plate, mPlate);
                    ReplaceSliceBestAnchor(plate, sliceHash, currentSlice, ref nextSliceId);
                }
                int currScore = PizzaSlice.GetSlicesSize(sliceHash.Values);
                improved = currScore > prevScore;
                prevScore = currScore;
            } while (improved);

            FillGaps(plate, sliceHash, ref nextSliceId);
            ExpandSlices(plate, sliceHash);

            int postScore = PizzaSlice.GetSlicesSize(sliceHash.Values);
            bool postImproved;
            do
            {
                List<PizzaSlice> slices = new List<PizzaSlice>(sliceHash.Values);
                foreach (PizzaSlice slice in slices)
                {
                    if (!sliceHash.TryGetValue(slice.ID, out PizzaSlice currentSlice)) continue;
                    sliceHash.Remove(currentSlice.ID);
                    currentSlice.RestoreSliceToPlate(plate, mPlate);
                    ReplaceSliceBestAnchor(plate, sliceHash, currentSlice, ref nextSliceId);
                }
                int curr = PizzaSlice.GetSlicesSize(sliceHash.Values);
                postImproved = curr > postScore;
                postScore = curr;
            } while (postImproved);            
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
                                if (mIsMedium)
                                {
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
                                else
                                {
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

        private void ExpandSlices(int[,] plate, Dictionary<int, PizzaSlice> sliceHash)
        {
            bool expanded;
            do
            {
                expanded = false;
                List<PizzaSlice> slices = sliceHash.Values.ToList();
                foreach (PizzaSlice slice in slices)
                {
                    if (!sliceHash.TryGetValue(slice.ID, out PizzaSlice current)) continue;

                    int nrMin = current.RowMin, nrMax = current.RowMax, ncMin = current.ColumnMin, ncMax = current.ColumnMax;

                    if (current.RowMin > 0)
                    {
                        bool allFree = true;
                        for (int cc = current.ColumnMin; cc <= current.ColumnMax; cc++)
                            if (plate[current.RowMin - 1, cc] <= 0) { allFree = false; break; }
                        if (allFree && IsValidSlice(mPlate, current.RowMin - 1, current.RowMax, current.ColumnMin, current.ColumnMax) == CHECK_SLICE_VALID)
                            nrMin = current.RowMin - 1;
                    }
                    if (current.RowMax < mRows - 1)
                    {
                        bool allFree = true;
                        for (int cc = current.ColumnMin; cc <= current.ColumnMax; cc++)
                            if (plate[current.RowMax + 1, cc] <= 0) { allFree = false; break; }
                        if (allFree && IsValidSlice(mPlate, current.RowMin, current.RowMax + 1, current.ColumnMin, current.ColumnMax) == CHECK_SLICE_VALID)
                            nrMax = current.RowMax + 1;
                    }
                    if (current.ColumnMin > 0)
                    {
                        bool allFree = true;
                        for (int rr = current.RowMin; rr <= current.RowMax; rr++)
                            if (plate[rr, current.ColumnMin - 1] <= 0) { allFree = false; break; }
                        if (allFree && IsValidSlice(mPlate, current.RowMin, current.RowMax, current.ColumnMin - 1, current.ColumnMax) == CHECK_SLICE_VALID)
                            ncMin = current.ColumnMin - 1;
                    }
                    if (current.ColumnMax < mColumns - 1)
                    {
                        bool allFree = true;
                        for (int rr = current.RowMin; rr <= current.RowMax; rr++)
                            if (plate[rr, current.ColumnMax + 1] <= 0) { allFree = false; break; }
                        if (allFree && IsValidSlice(mPlate, current.RowMin, current.RowMax, current.ColumnMin, current.ColumnMax + 1) == CHECK_SLICE_VALID)
                            ncMax = current.ColumnMax + 1;
                    }

                    if (nrMin != current.RowMin || nrMax != current.RowMax || ncMin != current.ColumnMin || ncMax != current.ColumnMax)
                    {
                        for (int r = current.RowMin; r <= current.RowMax; r++)
                            for (int c = current.ColumnMin; c <= current.ColumnMax; c++)
                                plate[r, c] = mPlate[r, c];
                        sliceHash.Remove(current.ID);
                        PizzaSlice expandedSlice = new PizzaSlice(current.ID, nrMin, nrMax, ncMin, ncMax);
                        expandedSlice.RemoveSliceFromPlate(plate);
                        sliceHash.Add(expandedSlice.ID, expandedSlice);
                        expanded = true;
                    }
                }
            } while (expanded);
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

            PizzaSlice maxSlice = GetMaxSliceExtentionAt(plate, sliceHash, r, c, nextSliceId, out _);
            if (maxSlice != null)
            {
                Dictionary<int, int> sliceContent = maxSlice.GetSliceContent(plate);
                foreach (int overlapSliceId in sliceContent.Keys)
                {
                    if (overlapSliceId > 0)
                        continue;

                    PizzaSlice existingSlice = sliceHash[overlapSliceId];
                    PizzaSlice existingAfterOverlap;

                    if (mIsMedium)
                    {
                        existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping_Generalized(maxSlice);
                    }
                    else
                    {
                        existingAfterOverlap = existingSlice.BuildShirnkedSliceWithOverlapping(maxSlice);
                        PizzaSlice gen = existingSlice.BuildShirnkedSliceWithOverlapping_Generalized(maxSlice);
                        if (existingAfterOverlap == null || (gen != null && gen.GetSize() > existingAfterOverlap.GetSize()))
                            existingAfterOverlap = gen;
                    }

                    if (existingAfterOverlap != null)
                    {
                        for (int cr = existingSlice.RowMin; cr <= existingSlice.RowMax; cr++)
                            for (int cc = existingSlice.ColumnMin; cc <= existingSlice.ColumnMax; cc++)
                                if (cr < existingAfterOverlap.RowMin || cr > existingAfterOverlap.RowMax ||
                                    cc < existingAfterOverlap.ColumnMin || cc > existingAfterOverlap.ColumnMax)
                                    plate[cr, cc] = mPlate[cr, cc];
                    }

                    sliceHash[existingSlice.ID] = existingAfterOverlap;
                }

                maxSlice.RemoveSliceFromPlate(plate);
                sliceHash.Add(maxSlice.ID, maxSlice);

                return true;
            }

            return false;
        }

        private PizzaSlice GetMaxSliceExtentionAt(int[,] plate, Dictionary<int, PizzaSlice> sliceHash, int row, int column, int nextSliceId, out int netGain)
        {
            netGain = 0;
            PizzaSlice bestSlice = null;
            int maxNetGain = 0;

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

                            int netGainCandidate = newSlice.GetSize();
                            Dictionary<int, int> sliceContent = newSlice.GetSliceContent(plate);
                            bool isValidOverlap = true;
                            foreach (int overlapSliceId in sliceContent.Keys)
                            {
                                if (overlapSliceId > 0)
                                    continue;

                                PizzaSlice existingSlice = sliceHash[overlapSliceId];
                                PizzaSlice existingAfterOverlap;

                                PizzaSlice afterOrig = existingSlice.BuildShirnkedSliceWithOverlapping(newSlice);
                                PizzaSlice afterGen = existingSlice.BuildShirnkedSliceWithOverlapping_Generalized(newSlice);
                                PizzaSlice bestAfter = null;
                                int bestLoss = int.MaxValue;
                                if (afterOrig != null && this.IsValidSlice(this.mPlate, afterOrig.RowMin, afterOrig.RowMax, afterOrig.ColumnMin, afterOrig.ColumnMax) == CHECK_SLICE_VALID)
                                {
                                    bestAfter = afterOrig;
                                    bestLoss = existingSlice.GetSize() - afterOrig.GetSize();
                                }
                                if (afterGen != null && this.IsValidSlice(this.mPlate, afterGen.RowMin, afterGen.RowMax, afterGen.ColumnMin, afterGen.ColumnMax) == CHECK_SLICE_VALID)
                                {
                                    int lossGen = existingSlice.GetSize() - afterGen.GetSize();
                                    if (bestAfter == null || lossGen < bestLoss) { bestAfter = afterGen; bestLoss = lossGen; }
                                }
                                existingAfterOverlap = bestAfter;

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

                                netGainCandidate -= (existingSlice.GetSize() - existingAfterOverlap.GetSize());
                            }
                            if (isValidOverlap == false)
                                continue;

                            if (netGainCandidate <= 0)
                                continue;

                            if (bestSlice == null || netGainCandidate > maxNetGain ||
                                (netGainCandidate == maxNetGain && newSlice.GetSize() < bestSlice.GetSize()))
                            {
                                bestSlice = newSlice;
                                maxNetGain = netGainCandidate;
                            }
                        }
                }

            if (bestSlice != null) netGain = maxNetGain;
            return bestSlice;
        }

        private void ReplaceSliceBestAnchor(int[,] plate, Dictionary<int, PizzaSlice> sliceHash, PizzaSlice oldSlice, ref int nextSliceId)
        {
            int bestGain = 0;
            PizzaSlice bestSlice = null;
            var anchors = new HashSet<Tuple<int, int>>();
            AddAnchorIfValid(plate, anchors, oldSlice.RowMin, oldSlice.ColumnMin);
            AddAnchorIfValid(plate, anchors, oldSlice.RowMax, oldSlice.ColumnMax);
            AddAnchorIfValid(plate, anchors, oldSlice.RowMin, oldSlice.ColumnMax);
            AddAnchorIfValid(plate, anchors, oldSlice.RowMax, oldSlice.ColumnMin);
            AddAnchorIfValid(plate, anchors, (oldSlice.RowMin + oldSlice.RowMax) / 2, (oldSlice.ColumnMin + oldSlice.ColumnMax) / 2);
            
            // Add up to 5 additional anchors at uncovered cells inside the old slice's bounding box
            int added = 0;
            for (int r = oldSlice.RowMin; r <= oldSlice.RowMax && added < 5; r++)
            {
                for (int c = oldSlice.ColumnMin; c <= oldSlice.ColumnMax && added < 5; c++)
                {
                    if (plate[r, c] > 0 && !anchors.Contains(Tuple.Create(r, c)))
                    {
                        anchors.Add(Tuple.Create(r, c));
                        added++;
                    }
                }
            }

            foreach (var anchor in anchors)
            {
                int ar = anchor.Item1, ac = anchor.Item2;
                PizzaSlice candidate = GetMaxSliceExtentionAt(plate, sliceHash, ar, ac, oldSlice.ID, out int gain);
                if (candidate != null && gain > bestGain)
                {
                    bestGain = gain;
                    bestSlice = candidate;
                }
            }
            if (bestSlice != null)
                SlicePizzaAtPosition(plate, bestSlice.RowMin, bestSlice.ColumnMin, sliceHash, bestSlice.ID);
        }

        private void AddAnchorIfValid(int[,] plate, HashSet<Tuple<int, int>> anchors, int r, int c)
        {
            if (r >= 0 && r < mRows && c >= 0 && c < mColumns && plate[r, c] > 0)
                anchors.Add(Tuple.Create(r, c));
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

            if ((maxRow - minRow + 1) * (maxCol - minCol + 1) > mMaxSliceSize)
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
