﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bonsai.Design;
using Bonsai;
using Bonsai.Vision.Design;
using OpenCV.Net;

[assembly: TypeVisualizer(typeof(ContoursMashupVisualizer), Target = typeof(VisualizerMashup<IplImageVisualizer, ContoursVisualizer>))]

namespace Bonsai.Vision.Design
{
    public class ContoursMashupVisualizer : DialogTypeVisualizer
    {
        IplImageVisualizer visualizer;

        public override void Show(object value)
        {
            var contours = (Contours)value;
            var image = visualizer.VisualizerImage;
            if (image != null && !contours.FirstContour.IsInvalid)
            {
                Core.cvDrawContours(image, contours.FirstContour, CvScalar.All(255), CvScalar.All(128), 2, 1, 8, CvPoint.Zero);
            }
        }

        public override void Load(IServiceProvider provider)
        {
            visualizer = (IplImageVisualizer)provider.GetService(typeof(DialogMashupVisualizer));
        }

        public override void Unload()
        {
        }
    }
}
