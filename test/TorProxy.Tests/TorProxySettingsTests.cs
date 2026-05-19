using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Nefarius.Utilities.TorProxy.Tests.TestSupport;
using Xunit;

namespace Nefarius.Utilities.TorProxy.Tests
{
    public class TorProxySettingsTests
    {
        private const int IntValue = 1234;
        private const string StringValue = "foo";
        private const bool BoolValue = true;

        [Theory]
        [MemberData(nameof(PropertiesData))]
        [DisplayTestMethodName]
        public void OldSetterSetsNewProperty(Property property)
        {
            var settings = new TorProxySettings();

            property.SetOld(settings, property.Value);

            Assert.Equal(property.Value, property.GetNew(settings));
        }

        [Theory]
        [MemberData(nameof(PropertiesData))]
        [DisplayTestMethodName]
        public void NewSetterSetsOldProperty(Property property)
        {
            var settings = new TorProxySettings();

            property.SetNew(settings, property.Value);

            Assert.Equal(property.Value, property.GetOld(settings));
        }

        [Theory]
        [MemberData(nameof(PropertiesData))]
        [DisplayTestMethodName]
        public void OldSetterHandlesNullNewSettings(Property property)
        {
            var settings = new TorProxySettings
            {
                PrivoxySettings = null!,
                TorSettings = null!,
            };

            property.SetOld(settings, property.Value);

            Assert.Equal(property.Value, property.GetOld(settings));
        }

        public static IEnumerable<object[]> PropertiesData => Properties
            .Select(x => new object[] { x });

        private static readonly IEnumerable<Property> Properties = new List<Property>
        {
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.HashedTorControlPassword,
                (x, y) => x.HashedTorControlPassword = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.TorSettings.HashedControlPassword,
                (x, y) => x.TorSettings.HashedControlPassword = y,
                StringValue),
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.PrivoxyPort,
                (x, y) => x.PrivoxyPort = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.PrivoxySettings.Port,
                (x, y) => x.PrivoxySettings.Port = y,
                IntValue),
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.TorControlPassword,
                (x, y) => x.TorControlPassword = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.TorSettings.ControlPassword,
                (x, y) => x.TorSettings.ControlPassword = y,
                StringValue),
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.TorControlPort,
                (x, y) => x.TorControlPort = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.TorSettings.ControlPort,
                (x, y) => x.TorSettings.ControlPort = y,
                IntValue),
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.TorDataDirectory,
                (x, y) => x.TorDataDirectory = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.TorSettings.DataDirectory,
                (x, y) => x.TorSettings.DataDirectory = y,
                StringValue),
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.TorExitNodes,
                (x, y) => x.TorExitNodes = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.TorSettings.ExitNodes,
                (x, y) => x.TorSettings.ExitNodes = y,
                StringValue),
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.TorSocksPort,
                (x, y) => x.TorSocksPort = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.TorSettings.SocksPort,
                (x, y) => x.TorSettings.SocksPort = y,
                IntValue),
            Property.Create(
#pragma warning disable CS0618 // Type or member is obsolete
                x => x.TorStrictNodes,
                (x, y) => x.TorStrictNodes = y,
#pragma warning restore CS0618 // Type or member is obsolete
                x => x.TorSettings.StrictNodes,
                (x, y) => x.TorSettings.StrictNodes = y,
                BoolValue),
        };

        public class Property
        {
            private Property(
                string name,
                Func<TorProxySettings, object> getOld,
                Action<TorProxySettings, object> setOld,
                Func<TorProxySettings, object> getNew,
                Action<TorProxySettings, object> setNew,
                object value)
            {
                Name = name;
                GetOld = getOld;
                SetOld = setOld;
                GetNew = getNew;
                SetNew = setNew;
                Value = value;
            }

            public string Name { get; }
            public Func<TorProxySettings, object> GetOld { get; }
            public Action<TorProxySettings, object> SetOld { get; }
            public Func<TorProxySettings, object> GetNew { get; }
            public Action<TorProxySettings, object> SetNew { get; }
            public object Value { get; }

            public override string ToString()
            {
                return Name;
            }

            public static Property Create<T>(
                Func<TorProxySettings, T> getOld,
                Action<TorProxySettings, T> setOld,
                Expression<Func<TorProxySettings, T>> getNew,
                Action<TorProxySettings, T> setNew,
                T value)
            {
                return new Property(
                    getNew.Body.ToString(),
                    x => getOld(x)!,
                    (x, y) => setOld(x, (T)y),
                    x => getNew.Compile()(x)!,
                    (x, y) => setNew(x, (T)y),
                    value!);
            }
        }
    }
}
