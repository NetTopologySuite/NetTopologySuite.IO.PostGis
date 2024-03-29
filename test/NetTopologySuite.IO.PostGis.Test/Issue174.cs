﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NetTopologySuite.IO.PostGis.Test
{
    class Issue174
    {
        [Test, Category("Issue174")]
        public void ensure_NetTopologySuite_IO_GeoJSON_assembly_is_strongly_named()
        {
            AssertStronglyNamedAssembly(typeof(PostGisReader));
        }

        private void AssertStronglyNamedAssembly(Type typeFromAssemblyToCheck)
        {
            Assert.That(typeFromAssemblyToCheck, Is.Not.Null, "Cannot determine assembly from null");
            var assembly = typeFromAssemblyToCheck.Assembly;
            Assert.That(assembly.FullName.Contains("PublicKeyToken=null"), Is.False, "Strongly named assembly should have a PublicKeyToken in fully qualified name");
        }
    }
}
