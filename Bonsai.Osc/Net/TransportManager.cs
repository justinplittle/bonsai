﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Bonsai.Osc.Net
{
    public static class TransportManager
    {
        public const string DefaultConfigurationFile = "Osc.config";
        static readonly Dictionary<string, Tuple<ITransport, RefCountDisposable>> openConnections = new Dictionary<string, Tuple<ITransport, RefCountDisposable>>();

        public static TransportDisposable ReserveConnection(string name)
        {
            Tuple<ITransport, RefCountDisposable> connection;
            if (!openConnections.TryGetValue(name, out connection))
            {
                var configuration = LoadConfiguration();
                if (!configuration.Contains(name))
                {
                    throw new ArgumentException("The specified connection name has no matching configuration.");
                }

                var transportConfiguration = configuration[name];
                var transport = transportConfiguration.CreateTransport();
                var dispose = Disposable.Create(() =>
                {
                    transport.Dispose();
                    openConnections.Remove(name);
                });

                var refCount = new RefCountDisposable(dispose);
                connection = Tuple.Create(transport, refCount);
                openConnections.Add(name, connection);
                return new TransportDisposable(transport, refCount);
            }

            return new TransportDisposable(connection.Item1, connection.Item2.GetDisposable());
        }

        public static TransportConfigurationCollection LoadConfiguration()
        {
            if (!File.Exists(DefaultConfigurationFile))
            {
                return new TransportConfigurationCollection();
            }

            var serializer = new XmlSerializer(typeof(TransportConfigurationCollection));
            using (var reader = XmlReader.Create(DefaultConfigurationFile))
            {
                return (TransportConfigurationCollection)serializer.Deserialize(reader);
            }
        }

        public static void SaveConfiguration(TransportConfigurationCollection configuration)
        {
            var serializer = new XmlSerializer(typeof(TransportConfigurationCollection));
            using (var writer = XmlWriter.Create(DefaultConfigurationFile, new XmlWriterSettings { Indent = true }))
            {
                serializer.Serialize(writer, configuration);
            }
        }
    }
}