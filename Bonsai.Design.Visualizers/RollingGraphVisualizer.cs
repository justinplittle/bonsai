﻿using Bonsai.Expressions;
using System;

namespace Bonsai.Design.Visualizers
{
    public class RollingGraphVisualizer : DialogTypeVisualizer
    {
        RollingGraphView view;
        RollingGraphBuilder.VisualizerController controller;
        static readonly TimeSpan TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30);
        DateTimeOffset updateTime;

        public RollingGraphVisualizer()
        {
            AutoScale = true;
            Capacity = 640;
            Max = 1;
        }

        public int Capacity { get; set; }

        public double Min { get; set; }

        public double Max { get; set; }

        public bool AutoScale { get; set; }

        public override void Show(object value)
        {
            var time = DateTime.Now;
            controller.Show(value, view);
            if ((time - updateTime) > TargetElapsedTime)
            {
                view.Graph.Invalidate();
                updateTime = time;
            }
        }

        public override void Load(IServiceProvider provider)
        {
            var context = (ITypeVisualizerContext)provider.GetService(typeof(ITypeVisualizerContext));
            var lineChartBuilder = (RollingGraphBuilder)ExpressionBuilder.GetVisualizerElement(context.Source).Builder;
            controller = lineChartBuilder.Controller;

            view = new RollingGraphView();
            view.Capacity = Capacity;
            view.AutoScale = AutoScale;
            if (!AutoScale)
            {
                view.Min = Min;
                view.Max = Max;
            }

            view.HandleDestroyed += delegate
            {
                Min = view.Min;
                Max = view.Max;
                AutoScale = view.AutoScale;
                Capacity = view.Capacity;
            };

            view.NumSeries = controller.NumSeries;
            view.Dock = System.Windows.Forms.DockStyle.Fill;
            GraphHelper.FormatOrdinalAxis(view.Graph.GraphPane.XAxis, controller.IndexType);
            GraphHelper.SetAxisLabel(view.Graph.GraphPane.XAxis, controller.IndexLabel);
            view.Graph.GraphPane.XAxis.ScaleFormatEvent += (graph, axis, value, index) =>
            {
                if (view.NumSeries == 0) return null;
                var series = graph.CurveList[0];
                return index < series.NPts ? series[index].Tag as string : null;
            };

            var visualizerService = (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
            if (visualizerService != null)
            {
                visualizerService.AddControl(view);
            }
        }

        public override void Unload()
        {
            view.Dispose();
            view = null;
            controller = null;
        }
    }
}
