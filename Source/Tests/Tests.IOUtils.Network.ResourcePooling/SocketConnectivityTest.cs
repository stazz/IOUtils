/*
 * Copyright 2018 Stanislav Muhametsin. All rights Reserved.
 *
 * Licensed  under the  Apache License,  Version 2.0  (the "License");
 * you may not use  this file  except in  compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed  under the  License is distributed on an "AS IS" BASIS,
 * WITHOUT  WARRANTIES OR CONDITIONS  OF ANY KIND, either  express  or
 * implied.
 *
 * See the License for the specific language governing permissions and
 * limitations under the License. 
 */
using IOUtils.Network.Configuration;
using IOUtils.Network.ResourcePooling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UtilPack.Tests.Network
{
   [TestClass]
   public class SocketConnectivityTest
   {
      [TestMethod
         , Timeout( 2000 )
         ]
      public async Task TestNormalSockets()
      {
         var port = new Random().Next( 2000, 50000 );
         await TestWithServer(
            new IPEndPoint( IPAddress.Loopback, port ),
            ProtocolType.Tcp,
            new TestConnectionConfig()
            {
               Host = IPAddress.Loopback.ToString(),
               Port = port
            } );
      }

      [TestMethod
         , Timeout( 1000 )
         ]
      public async Task TestUnixSockets()
      {
         const String unixSocketPath = "/var/run/some_socket";
         await TestWithServer(
            new UnixDomainSocketEndPoint( unixSocketPath ),
            ProtocolType.IP,
            new TestConnectionConfig()
            {
               UnixSocketFilePath = unixSocketPath
            } );
      }

      private static async Task TestWithServer(
         EndPoint serverEP,
         ProtocolType protocolType,
         TestConnectionConfig clientConfig
         )
      {
         var serverCanDisconnect = 0;
         await Task.WhenAll(
            RunServer( serverEP, protocolType, () => serverCanDisconnect != 0 ),
            RunClient( clientConfig, () => Interlocked.Exchange( ref serverCanDisconnect, 1 ) )
            );
      }

      private static async Task RunServer(
         EndPoint serverEP,
         ProtocolType protocolType,
         Func<Boolean> shouldDisconnectClient
         )
      {
         using ( var server = new Socket( serverEP.AddressFamily, SocketType.Stream, protocolType ) )
         {
            server.Bind( serverEP );
            server.Listen( 1 );

            using ( var incoming = await server.AcceptAsync() )
            {
               while ( !shouldDisconnectClient() )
               {
                  await Task.Delay( 50 );
               }

               incoming.Disconnect( false );
            }
         }
      }

      private static async Task RunClient(
         TestConnectionConfig clientConfig,
         Action serverCanDisconnect
         )
      {
         var config = new TestNetworkConfig( new TestNetworkConfigData()
         {
            Connection = clientConfig
         } );
         var nwConfig = config.CreateNetworkStreamFactoryConfiguration(
            Encoding.UTF8,
            () => TaskUtils.False,
            null,
            null,
            null,
            null,
            null
            );
         (var socket, var stream) = await NetworkStreamFactory.AcquireNetworkStreamFromConfiguration( nwConfig.Item1, default );
         Assert.IsNotNull( socket, "Socket must not be null." );
         Assert.IsTrue( socket.Connected, "Socket must be connected." );
         stream.DisposeSafely();
         serverCanDisconnect();
      }

   }


   public sealed class TestNetworkConfig : NetworkConnectionCreationInfo<TestNetworkConfigData, TestConnectionConfig, TestInitConfig, TestProtocolConfig, TestPoolConfig>
   {
      public TestNetworkConfig(
         TestNetworkConfigData data
         ) : base( data )
      {
      }
   }

   public sealed class TestNetworkConfigData : NetworkConnectionCreationInfoData<TestConnectionConfig, TestInitConfig, TestProtocolConfig, TestPoolConfig>
   {

   }

   public sealed class TestConnectionConfig : NetworkConnectionConfiguration
   {

   }

   public sealed class TestInitConfig : NetworkInitializationConfiguration<TestProtocolConfig, TestPoolConfig>
   {

   }

   public sealed class TestProtocolConfig
   {

   }

   public sealed class TestPoolConfig : NetworkPoolingConfiguration
   {

   }
}
