using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Torino.Tests
{
	public class ControlSocketTests
	{
		[Fact]
		public async Task WellformedTest()
		{
			var network = new MemoryStream();
			var sut = new ControlSocket(network);

			network.RespondWith("250 OK\r\n");
			var response = await sut.ReceiveAsync();
			var entry = Assert.Single(response.Entries);
			Assert.Equal(ReplyCode.OK, entry.StatusCode);
			Assert.Equal(  " ", entry.Divider);
			Assert.Equal( "OK", entry.Content);

			network.RespondWith(
				"250+info/names=desc/id/* -- Router descriptors by ID.\r\n" + 
				"desc/name/* -- Router descriptors by nickname.\r\n" +
				".\r\n" +
				"250 OK\r\n");
			response = await sut.ReceiveAsync();
			entry = Assert.Single(response.Entries);
			Assert.Equal(ReplyCode.OK, entry.StatusCode);
			Assert.Equal(  "+", entry.Divider);
			Assert.Contains("\n", entry.Content);
			response = await sut.ReceiveAsync();
			entry = Assert.Single(response.Entries);
			Assert.Equal(ReplyCode.OK, entry.StatusCode);

			network.RespondWith(
				"250-PROTOCOLINFO 1\r\n" +
				"250-AUTH METHODS=COOKIE,SAFECOOKIE COOKIEFILE=\"/home/user/.tor/control_auth_cookie\"\r\n" +
				"250-VERSION Tor=\"0.2.5.1-alpha-dev\"\r\n" +
				"250 OK\r\n");
			response = await sut.ReceiveAsync();
			var entries = response.Entries;
			Assert.Equal(ReplyCode.OK, entries[0].StatusCode);
			Assert.Equal(  "-", entries[0].Divider);
			Assert.Equal("PROTOCOLINFO 1", entries[0].Content);
			Assert.Equal(ReplyCode.OK, entries[1].StatusCode);
			Assert.Equal(  "-", entries[1].Divider);
			Assert.Equal("AUTH METHODS=COOKIE,SAFECOOKIE COOKIEFILE=\"/home/user/.tor/control_auth_cookie\"", entries[1].Content);
			Assert.Equal(ReplyCode.OK, entries[2].StatusCode);
			Assert.Equal(  "-", entries[2].Divider);
			Assert.Equal("VERSION Tor=\"0.2.5.1-alpha-dev\"", entries[2].Content);
		}

		[Fact]
		public async Task MalformedLineTest()
		{
			var network = new MemoryStream();
			var sut = new ControlSocket(network);

			network.RespondWith("20");
			var ex = await Assert.ThrowsAsync<ProtocolException>(async () => await sut.ReceiveAsync());
			Assert.EndsWith("beginning is malformed.", ex.Message);

			network.RespondWith("");
			ex = await Assert.ThrowsAsync<ProtocolException>(async () => await sut.ReceiveAsync());
			Assert.Equal("Received empty socket content.", ex.Message);

			network.RespondWith(
				"250+info/names=desc/id/* -- Router descriptors by ID.\r\n" + 
				"desc/name/* -- Router descriptors by nickname.\r\n" +
				"\r\n" +
				"250 OK\r\n");
			ex = await Assert.ThrowsAsync<ProtocolException>(async () => await sut.ReceiveAsync());
			Assert.Equal("Received empty socket content.", ex.Message);
		}
	}

	public static class StreamExtensions
	{
		public static void RespondWith(this Stream stream, string text)
		{
			var buffer = Encoding.UTF8.GetBytes(text);
			stream.Write(buffer);
			stream.Seek(-buffer.Length, SeekOrigin.Current);
		}
	}

}