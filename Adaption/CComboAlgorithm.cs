﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using LiniarAlgebra;
using PCA;
using ShapeContext;
using HausdorffDistance;

namespace Adaption
{
    public class CComboAlgorithm : AlgorithmBase
    {
        public static readonly int sr_MaxCol = 1;

        private Image m_Image1 = null;
        private Image m_Image2 = null;

        private List<Point> m_SourceBank = null;
        private List<Point> m_TargetBank = null;

        private Point[] m_sourcePtArray = null;
        private Point[] m_targetPtArray = null;

        public CComboAlgorithm()
        {
            ShapeContextSamplesNum = sr_Not_Set;
            ShapeContextWarpDistanceTreshold = sr_Not_Set;
        }

        #region AlgorithmBase members

        public override void Create(Image i_SourceImage, Image i_TargetImage)
        {
            m_Image1 = i_SourceImage;
            m_Image2 = i_TargetImage;
        }

        public override Type MyType
        {
            get
            {
                return typeof(CComboAlgorithm);
            }
        }

        public override ICData Run()
        {
            Size StartingCommonSize = new Size(Math.Max(m_Image1.Width, m_Image2.Width), Math.Max(m_Image1.Height, m_Image2.Height));
            //Preparing structures
            List<Point> sourcePoints = Utilities.ExtractPoints(m_Image1, TresholdColor);
            List<Point> targetPoints = Utilities.ExtractPoints(m_Image2, TresholdColor);

            DoubleMatrix source = Utilities.ListToMatrix(sourcePoints);
            DoubleMatrix target = Utilities.ListToMatrix(targetPoints);

            //1st station, PCA alignment
            ////////////////////////////
            PCAMatching pcaMatching = new PCAMatching(source, target);
            pcaMatching.Calculate();

            target = pcaMatching.Result;
            
            //In between stages
            DoubleMatrix minMax = PCA.Utils.ShiftToPositives(ref target, source);
            minMax              = PCA.Utils.ShiftToPositives(ref source, target);

            sourcePoints = Utilities.MatrixToList(source);
            targetPoints = Utilities.MatrixToList(target);

            m_sourcePtArray = sourcePoints.ToArray();
            m_targetPtArray = targetPoints.ToArray();

            Size meshSize = new Size(
                Math.Max((int)Math.Ceiling(minMax[sr_X, sr_MaxCol] + 2), StartingCommonSize.Width),
                Math.Max((int)Math.Ceiling(minMax[sr_Y, sr_MaxCol] + 2), StartingCommonSize.Height));

            //2nd station,Hausdorff Matching Points insertion
            //////////////////////////////////////////////////
            IntMatrix sourceBinaryMap = Utilities.ToBinaryMap(source,meshSize);
            IntMatrix targetBinaryMap = Utilities.ToBinaryMap(target,meshSize);

            HausdorffMatching hausdorffMatching = new HausdorffMatching(sourceBinaryMap, targetBinaryMap);
            IntMatrix diffSource = hausdorffMatching.Calculate1on2();
            IntMatrix diffTarget = hausdorffMatching.Calculate2on1();

            //Preparing a logic for point selection bank
            List<Point> currList = null;
            Func<int, int, int, int> pointInsertionLogic = (row, col, value) =>
                {
                    for (int i = 2; i < value; ++i)
                    {
                        currList.Add(new Point(col, row));
                    }
                    return value;
                };

            //Applying this logic
            m_SourceBank = new List<Point>();
            currList = m_SourceBank;
            diffSource.Iterate(pointInsertionLogic);
            m_TargetBank = new List<Point>();
            currList = m_TargetBank;
            diffTarget.Iterate(pointInsertionLogic);

            //3rd station ShapeContext Matching
            ///////////////////////////////////
            ShapeContextMatching shapeContextMatching = new ShapeContextMatching(m_sourcePtArray, m_targetPtArray, meshSize, SelectSamplesLogic);
            
            shapeContextMatching.AlignmentLogic = shapeContextMatching.StandardAlignmentLogic;
            if (ShapeContextWarpDistanceTreshold > 0)
            {
                shapeContextMatching.DistanceTreshold = ShapeContextWarpDistanceTreshold;
            }
            shapeContextMatching.Calculate();

            CShapeContextResultData retResult =
                new CShapeContextResultData(
                    m_sourcePtArray,
                    m_targetPtArray,
                    meshSize,
                    shapeContextMatching.LastSourceSamples,
                    shapeContextMatching.LastTargetSamples);

            return retResult;
        }

        private Point[] SelectSamplesLogic(Point[] i_FullSet, int i_numOfPoints)
        {
            Point[] currBank = null;
            if (i_FullSet == m_sourcePtArray)
            {
                currBank = m_SourceBank.ToArray();
            }
            else if (i_FullSet == m_targetPtArray)
            {
                currBank = m_TargetBank.ToArray();
            }

            if (i_numOfPoints > 0)
            {
                return ShapeContext.Utils.GetIndexedSamplePoints(currBank, i_numOfPoints);
            }
            else
            {
                int numberOfSamples = 0;

                if (ShapeContextSamplesNum == sr_Not_Set)
                {
                    numberOfSamples = Math.Min(m_SourceBank.Count, m_TargetBank.Count) / 50;
                }
                else
                {
                    numberOfSamples = ShapeContextSamplesNum;
                }                 
                return ShapeContext.Utils.GetIndexedSamplePoints(i_FullSet, numberOfSamples);
            }
        }

        public override object Instance
        {
            get
            {
                return this;
            }
        }

        #endregion

        #region Props
        public int ShapeContextSamplesNum { get; set; }
        public int ShapeContextWarpDistanceTreshold { get; set; }
        #endregion
    }
}
