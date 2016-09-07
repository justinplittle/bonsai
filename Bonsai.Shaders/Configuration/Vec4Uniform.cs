﻿using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Shaders.Configuration
{
    public class Vec4Uniform : UniformConfiguration
    {
        [TypeConverter("OpenCV.Net.NumericAggregateConverter, OpenCV.Net")]
        [Description("The value used to initialize the uniform variable.")]
        public Vector4 Value { get; set; }

        internal override void SetUniform(int location)
        {
            GL.Uniform4(location, Value);
        }

        public override string ToString()
        {
            return string.Format("{0}{1}", string.IsNullOrEmpty(Name) ? "Vec4" : Name, Value);
        }
    }
}