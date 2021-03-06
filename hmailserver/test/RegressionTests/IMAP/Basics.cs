// Copyright (c) 2010 Martin Knafve / hMailServer.com.  
// http://www.hmailserver.com

using System;
using System.Text;
using NUnit.Framework;
using RegressionTests.Infrastructure;
using RegressionTests.Shared;
using hMailServer;

namespace RegressionTests.IMAP
{
   [TestFixture]
   public class Basics : TestFixtureBase
   {
      [Test]
      public void TestAppendBadLiteral()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.LogonWithLiteral("check@test.com", "test");
         oSimulator.SendSingleCommandWithLiteral("A01 APPEND INBOX {TEST}", "ABCD");
         Assert.AreEqual(0, oSimulator.GetMessageCount("INBOX"));
         oSimulator.Disconnect();
      }

      [Test]
      public void TestAppendDeletedMessage()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("check@test.com", "test");
         oSimulator.SendSingleCommandWithLiteral("A01 APPEND INBOX (\\Deleted) {4}", "ABCD");
         Assert.AreEqual(1, oSimulator.GetMessageCount("INBOX"));

         Assert.AreEqual("1", oSimulator.Search("DELETED"));

         oSimulator.Disconnect();
      }

      [Test]
      [Description("Test that one can use APPEND and specify the folder name separately using {}-notation")]
      public void TestAppendFolderNameInOctet()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("check@test.com", "test");
         oSimulator.SelectFolder("INBOX");
         oSimulator.CreateFolder("MONK");
         oSimulator.SendRaw("A01 APPEND {4}\r\n");
         string result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for additional command text."));

         oSimulator.SendRaw("MONK (\\Seen) \"20-Jan-2009 12:59:50 +0100\" {5}\r\n");
         result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for literal data"));

         oSimulator.SendRaw("WOOOT\r\n");
         result = oSimulator.Receive();

         Assert.AreEqual("A01 OK APPEND completed\r\n", result);
      }

      [Test]
      [Description(
         "Test that one can use APPEND and specify the folder name separately using {}-notation. Do not include flag list (it's optional)"
         )]
      public void TestAppendFolderNameInOctetNoFlagList()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("check@test.com", "test");
         oSimulator.SelectFolder("INBOX");
         oSimulator.CreateFolder("MONK");
         oSimulator.SendRaw("A01 APPEND {4}\r\n");
         string result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for additional command text."));

         oSimulator.SendRaw("MONK  \"12-Jan-2009 12:12:12 +0100\" {5}\r\n");
         result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for literal data"));

         oSimulator.SendRaw("WOOOT\r\n");
         result = oSimulator.Receive();

         Assert.AreEqual("A01 OK APPEND completed\r\n", result);

         DateTime date = Convert.ToDateTime(oAccount.IMAPFolders.get_ItemByName("MONK").Messages[0].InternalDate);

         Assert.AreEqual(2009, date.Year);
         Assert.AreEqual(12, date.Day);
         Assert.AreEqual(1, date.Month);
      }

      [Test]
      [Description(
         "Test that one can use APPEND and specify the folder name separately using {}-notation. Do not include flag list or timestamp (it's optional)"
         )]
      public void TestAppendFolderNameInOctetNoFlagListOrDate()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("check@test.com", "test");
         oSimulator.SelectFolder("INBOX");
         oSimulator.CreateFolder("MONK");
         oSimulator.SendRaw("A01 APPEND {4}\r\n");
         string result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for additional command text."));

         oSimulator.SendRaw("MONK {5}\r\n");
         result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for literal data"));

         oSimulator.SendRaw("WOOOT\r\n");
         result = oSimulator.Receive();

         Assert.AreEqual("A01 OK APPEND completed\r\n", result);
      }

      [Test]
      [Description(
         "Test that one can use APPEND and specify the folder name separately using {}-notation. Do not include timestamp but set deleted flag."
         )]
      public void TestAppendFolderNameInOctetSetDeletedFlag()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("check@test.com", "test");
         oSimulator.SelectFolder("INBOX");
         oSimulator.CreateFolder("MONK");
         oSimulator.SendRaw("A01 APPEND {4}\r\n");
         string result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for additional command text."));

         oSimulator.SendRaw("MONK (\\Seen \\Deleted) {5}\r\n");
         result = oSimulator.Receive();
         Assert.IsTrue(result.StartsWith("+ Ready for literal data"));

         oSimulator.SendRaw("WOOOT\r\n");
         result = oSimulator.Receive();

         Assert.AreEqual("A01 OK APPEND completed\r\n", result);
      }

      [Test]
      [Description("Issue 247: IMAP: Untagged EXISTS not sent after APPEND completion.")]
      public void TestAppendResponseContainsExists()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.LogonWithLiteral("check@test.com", "test");
         Assert.IsTrue(oSimulator.SelectFolder("Inbox"));
         string response1 = oSimulator.SendSingleCommandWithLiteral("A01 APPEND INBOX {4}", "ABCD");
         Assert.IsTrue(response1.Contains("* 1 EXISTS"), response1);
         Assert.IsTrue(response1.Contains("* 1 RECENT"), response1);
         Assert.AreEqual(1, oSimulator.GetMessageCount("INBOX"));
         oSimulator.Disconnect();
      }

      [Test]
      public void TestAuthenticate()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "literal@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         string result = oSimulator.SendSingleCommand("A01 AUTHENTICATE");
         Assert.IsTrue(result.Contains("NO Unsupported authentication mechanism."));
         oSimulator.Disconnect();
      }

      [Test]
      public void TestBeforeLogon()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "delete@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();

         Assert.IsTrue(oSimulator.ExamineFolder("NonexistantFolder").Contains("NO Authenticate first"));
         Assert.IsFalse(oSimulator.SelectFolder("NonexistantFolder"));
         Assert.IsFalse(oSimulator.Copy(1, "SomeFolder"));
         Assert.IsFalse(oSimulator.CheckFolder("SomeFolder"));
         Assert.IsTrue(oSimulator.Fetch("123 a").Contains("NO Authenticate first"));
         Assert.IsTrue(oSimulator.List().Contains("NO Authenticate first"));
         Assert.IsTrue(oSimulator.LSUB().Contains("NO Authenticate first"));
         Assert.IsTrue(oSimulator.GetMyRights("APA").Contains("NO Authenticate first"));
         Assert.IsFalse(oSimulator.RenameFolder("A", "B"));
         Assert.IsFalse(oSimulator.Status("SomeFolder", "MESSAGES").Contains("A01 OK"));
      }

      [Test]
      [Description("Issue 218, IMAP: Problem with file name containing non-latin chars. In this test they are encoded"
         )]
      public void TestBodyStructureWithNonLatinCharacterInAttachmentHeader()
      {
         string @messageText =
            "From: \"Test\" <test@test.com>" + "\r\n" +
            "To: \"Test\" <test@test.com>" + "\r\n" +
            "Subject: test" + "\r\n" +
            "MIME-Version: 1.0" + "\r\n" +
            "Content-Type: multipart/mixed;" + "\r\n" +
            "   boundary=\"----=_NextPart_000_000C_01C9EEB2.08D2EC80\"" + "\r\n" +
            "X-Priority: 3" + "\r\n" +
            "" + "\r\n" +
            "This is a multi-part message in MIME format." + "\r\n" +
            "" + "\r\n" +
            "------=_NextPart_000_000C_01C9EEB2.08D2EC80" + "\r\n" +
            "Content-Type: text/plain;" + "\r\n" +
            "  format=flowed;" + "\r\n" +
            "	charset=\"iso-8859-1\";" + "\r\n" +
            "	reply-type=original" + "\r\n" +
            "Content-Transfer-Encoding: 7bit" + "\r\n" +
            "" + "\r\n" +
            "" + "\r\n" +
            "------=_NextPart_000_000C_01C9EEB2.08D2EC80" + "\r\n" +
            "Content-Type: application/octet-stream;" + "\r\n" +
            "	name=\"=?iso-8859-1?B?beT2LnppcA==?=\"" + "\r\n" +
            "Content-Transfer-Encoding: base64" + "\r\n" +
            "Content-Disposition: attachment;" + "\r\n" +
            "	filename=\"=?iso-8859-1?B?beT2LnppcA==?=\"" + "\r\n" +
            "" + "\r\n" +
            "iVBORw0KGgoAAAANSUhEUgAAAqgAAAH4CAIAAAAJvIhhAAAAAXNSR0IArs4c6QAAAARnQU1BAACx" + "\r\n" +
            "uIDgj5MrSIAAAQIEzgkI/nP2KhMgQIAAgbiA4I+TK0iAAAECBM4JCP5z9ioTIECAAIG4wP8ChvJS" + "\r\n" +
            "wXUaKVoAAAAASUVORK5CYII=" + "\r\n" +
            "" + "\r\n" +
            "------=_NextPart_000_000C_01C9EEB2.08D2EC80--" + "\r\n";

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSendRaw(account.Address, account.Address, messageText);

         CustomAsserts.AssertFolderMessageCount(account.IMAPFolders[0], 1);

         var oSimulator = new ImapClientSimulator();
         oSimulator.ConnectAndLogon(account.Address, "test");
         oSimulator.SelectFolder("INBOX");
         string result = oSimulator.Fetch("1 BODYSTRUCTURE");
         oSimulator.Disconnect();

         Assert.IsTrue(result.Contains("(\"NAME\" \"=?iso-8859-1?B?beT2LnppcA==?=\")"));
         Assert.IsTrue(result.Contains("(\"FILENAME\" \"=?iso-8859-1?B?beT2LnppcA==?=\")"));

         string fileName = account.IMAPFolders.get_ItemByName("INBOX").Messages[0].Attachments[0].Filename;
         Assert.AreEqual("mäö.zip", fileName);
      }

      [Test]
      [Description("Issue 218, IMAP: Problem with file name containing non-latin chars (RFC 2184 compliance)")]
      public void TestBodyStructureWithNonLatinCharacterMultiLine()
      {
         string @messageText =
            "To: test@test.com\r\n" +
            "Content-Type: multipart/mixed;\r\n" +
            " boundary=\"------------000008080307000003010005\"\r\n" +
            "\r\n" +
            "This is a multi-part message in MIME format.\r\n" +
            "--------------000008080307000003010005\r\n" +
            "Content-Type: text/plain; charset=ISO-8859-1; format=flowed\r\n" +
            "Content-Transfer-Encoding: 7bit\r\n" +
            "\r\n" +
            "Test\r\n" +
            "\r\n" +
            "--------------000008080307000003010005\r\n" +
            "Content-Type: image/png;\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "Content-Disposition: inline;\r\n" +
            " filename*0*=ISO-8859-1''%F6%50%C4%C9%CD%C1%D6%60%F6%F6%E4%27%20%31%20%F6;\r\n" +
            " filename*1*=\"%50%C4%C9%CD%C1%D6%60%F6%F6%E4%27%20%32%20%F6%50%C4%C9%CD%C1\";\r\n" +
            " filename*2*=%D6%60%F6%F6%E4%27%20%33%2E%70%6E%67;\r\n" +
            "\r\n" +
            "iVBORw0KGgoAAAANSUhEUgAAAqgAAAH4CAIAAAAJvIhhAAAAAXNSR0IArs4c6QAAAARnQU1B\r\n" +
            "AACxjwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAA\r\n" +
            "--------------000008080307000003010005--\r\n";

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSendRaw(account.Address, account.Address, messageText);

         CustomAsserts.AssertFolderMessageCount(account.IMAPFolders[0], 1);

         var oSimulator = new ImapClientSimulator();
         oSimulator.ConnectAndLogon(account.Address, "test");
         oSimulator.SelectFolder("INBOX");
         string result = oSimulator.Fetch("1 BODYSTRUCTURE");
         oSimulator.Disconnect();

         Assert.IsTrue(
            result.Contains(
               "\"FILENAME\" \"=?ISO-8859-1?Q?=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=20=31=20=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=20=32=20=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=20=33=2E=70=6E=67?=\""));
         Assert.IsTrue(
            result.Contains(
               "\"NAME\" \"=?ISO-8859-1?Q?=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=20=31=20=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=20=32=20=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=20=33=2E=70=6E=67?=\""));
      }

      [Test]
      [Description(
         "Issue 218, IMAP: Problem with file name containing non-latin chars (RFC 2184 compliance). In this test, there's only one line of 2184-encoded data."
         )]
      public void TestBodyStructureWithNonLatinCharacterSingleLine()
      {
         string @messageText =
            "Return-Path: martin@hmailserver.com\r\n" +
            "Delivered-To: martin@hmailserver.com\r\n" +
            "Received: from www.hmailserver.com ([127.0.0.1])\r\n" +
            "	by mail.hmailserver.com\r\n" +
            "	; Tue, 16 Jun 2009 21:39:18 +0200\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Date: Tue, 16 Jun 2009 21:39:18 +0200\r\n" +
            "From: <martin@hmailserver.com>\r\n" +
            "To: <martin@hmailserver.com>\r\n" +
            "Subject: sdafsda\r\n" +
            "Message-ID: <96aee740f2abe8450648c1752a9a987b@localhost>\r\n" +
            "X-Sender: martin@hmailserver.com\r\n" +
            "User-Agent: RoundCube Webmail/0.2.2\r\n" +
            "Content-Type: multipart/mixed;\r\n" +
            "	boundary=\"=_b63968892a76b1a5be17f4d37b085f54\"\r\n" +
            "\r\n" +
            "--=_b63968892a76b1a5be17f4d37b085f54\r\n" +
            "Content-Transfer-Encoding: 8bit\r\n" +
            "Content-Type: text/plain; charset=\"UTF-8\"\r\n" +
            "\r\n" +
            "--=_b63968892a76b1a5be17f4d37b085f54\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "Content-Type: application/x-zip; charset=\"UTF-8\";\r\n" +
            " name*=\"UTF-8''m%C3%A4%C3%B6.zip\"; \r\n" +
            "Content-Disposition: attachment;\r\n" +
            " filename*=\"UTF-8''m%C3%A4%C3%B6.zip\"; \r\n" +
            "\r\n" +
            "iVBORw0KGgoAAAANSUhEUgAAAqgAAAH4CAIAAAAJvIhhAAAAAXNSR0IArs4c6QAAAARnQU1BAACx\r\n" +
            "jwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAAIr1JREFU\r\n" +
            "eF7t29GSYzluBND1/3/0ejb6wY6emK2WrpQEkaefdQvAAcn02OH/+fe///0v/wgQIECAAIESgb+C\r\n" +
            "3z8CBAgQIECgROBfJXMakwABAgQIEPjP/5qfAgECBAgQINAjIPh7dm1SAgQIECDgv/idAQIECBAg\r\n" +
            "--=_b63968892a76b1a5be17f4d37b085f54--\r\n" +
            "";

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSendRaw(account.Address, account.Address, messageText);

         CustomAsserts.AssertFolderMessageCount(account.IMAPFolders[0], 1);


         var oSimulator = new ImapClientSimulator();
         oSimulator.ConnectAndLogon(account.Address, "test");
         oSimulator.SelectFolder("INBOX");
         string result = oSimulator.Fetch("1 BODYSTRUCTURE");
         oSimulator.Disconnect();

         Assert.IsFalse(result.Contains("''"), result);
         Assert.IsTrue(result.Contains("\"FILENAME\" \"=?UTF-8?Q?m=C3=A4=C3=B6.zip?=\""), result);
         Assert.IsTrue(result.Contains("\"NAME\" \"=?UTF-8?Q?m=C3=A4=C3=B6.zip?=\""), result);
      }

      [Test]
      [Description("Issue 218, IMAP: Problem with file name containing non-latin chars. In this test they are encoded"
         )]
      public void TestBodyStructureWithNonLatinCharacterSingleLineEncoded()
      {
         string @messageText =
            "Message-ID: <1d11306c5648497247447e1073c3b0e2.squirrel@www.*******.**>\r\n" +
            "Date: Fri, 29 May 2009 11:53:03 +0200\r\n" +
            "Subject: attachment's name test\r\n" +
            "From: martin@hmailserver.com\r\n" +
            "To: martin@hmailserver.com\r\n" +
            "User-Agent: SquirrelMail/1.4.19\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: multipart/mixed;boundary=\"----=_20090529115303_60479\"\r\n" +
            "X-Priority: 3 (Normal)\r\n" +
            "Importance: Normal\r\n" +
            "\r\n" +
            "------=_20090529115303_60479\r\n" +
            "Content-Type: text/plain; charset=\"iso-8859-2\"\r\n" +
            "Content-Transfer-Encoding: 8bit\r\n" +
            "\r\n" +
            "test.±æê³ñó¶¼¿.txt\r\n" +
            "------=_20090529115303_60479\r\n" +
            "Content-Type: text/plain; name=\r\n" +
            "    =?iso-8859-2?Q?test.=B1=E6=EA=B3=F1=F3=B6=BC=BF.txt?=\r\n" +
            "Content-Transfer-Encoding: 8bit\r\n" +
            "Content-Disposition: attachment; filename=\"\r\n" +
            "    =?iso-8859-2?Q?test.=B1=E6=EA=B3=F1=F3=B6=BC=BF.txt?=\"\r\n" +
            "\r\n" +
            "1234\r\n" +
            "------=_20090529115303_60479--\r\n";


         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSendRaw(account.Address, account.Address, messageText);

         CustomAsserts.AssertFolderMessageCount(account.IMAPFolders[0], 1);

         var oSimulator = new ImapClientSimulator();
         oSimulator.ConnectAndLogon(account.Address, "test");
         oSimulator.SelectFolder("INBOX");
         string result = oSimulator.Fetch("1 BODYSTRUCTURE");
         oSimulator.Disconnect();

         Assert.IsTrue(result.Contains("(\"NAME\" \"=?iso-8859-2?Q?test.=B1=E6=EA=B3=F1=F3=B6=BC=BF.txt?=\")"));
         Assert.IsTrue(result.Contains("(\"FILENAME\" \"=?iso-8859-2?Q?test.=B1=E6=EA=B3=F1=F3=B6=BC=BF.txt?=\")"));
      }

      [Test]
      [Description(
         "Issue 218, IMAP: Problem with file name containing non-latin chars (RFC 2184 compliance). In this test, there are spaces in the 2184-encoded data."
         )]
      public void TestBodyStructureWithNonLatinCharacterSingleLineWithSpace()
      {
         string @messageText =
            "Return-Path: martin@hmailserver.com\r\n" +
            "Delivered-To: martin@hmailserver.com\r\n" +
            "Received: from www.hmailserver.com ([127.0.0.1])\r\n" +
            "	by mail.hmailserver.com\r\n" +
            "	; Tue, 16 Jun 2009 21:39:18 +0200\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Date: Tue, 16 Jun 2009 21:39:18 +0200\r\n" +
            "From: <martin@hmailserver.com>\r\n" +
            "To: <martin@hmailserver.com>\r\n" +
            "Subject: sdafsda\r\n" +
            "Message-ID: <96aee740f2abe8450648c1752a9a987b@localhost>\r\n" +
            "X-Sender: martin@hmailserver.com\r\n" +
            "User-Agent: RoundCube Webmail/0.2.2\r\n" +
            "Content-Type: multipart/mixed;\r\n" +
            "	boundary=\"=_b63968892a76b1a5be17f4d37b085f54\"\r\n" +
            "\r\n" +
            "--=_b63968892a76b1a5be17f4d37b085f54\r\n" +
            "Content-Transfer-Encoding: 8bit\r\n" +
            "Content-Type: text/plain; charset=\"UTF-8\"\r\n" +
            "\r\n" +
            "--=_b63968892a76b1a5be17f4d37b085f54\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "Content-Type: application/x-zip; charset=\"UTF-8\";\r\n" +
            " name*=\"UTF-8''m%C3%A4%C3%B6 m%C3%A4%C3%B6.zip\"; \r\n" +
            "Content-Disposition: attachment;\r\n" +
            " filename*=\"UTF-8''m%C3%A4%C3%B6 m%C3%A4%C3%B6.zip\"; \r\n" +
            "\r\n" +
            "iVBORw0KGgoAAAANSUhEUgAAAqgAAAH4CAIAAAAJvIhhAAAAAXNSR0IArs4c6QAAAARnQU1BAACx\r\n" +
            "jwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAAIr1JREFU\r\n" +
            "eF7t29GSYzluBND1/3/0ejb6wY6emK2WrpQEkaefdQvAAcn02OH/+fe///0v/wgQIECAAIESgb+C\r\n" +
            "3z8CBAgQIECgROBfJXMakwABAgQIEPjP/5qfAgECBAgQINAjIPh7dm1SAgQIECDgv/idAQIECBAg\r\n" +
            "--=_b63968892a76b1a5be17f4d37b085f54--\r\n" +
            "";

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSendRaw(account.Address, account.Address, messageText);

         CustomAsserts.AssertFolderMessageCount(account.IMAPFolders[0], 1);


         var oSimulator = new ImapClientSimulator();
         oSimulator.ConnectAndLogon(account.Address, "test");
         oSimulator.SelectFolder("INBOX");
         string result = oSimulator.Fetch("1 BODYSTRUCTURE");
         oSimulator.Disconnect();

         Assert.IsFalse(result.Contains("''"), result);
         Assert.IsTrue(result.Contains("\"FILENAME\" \"=?UTF-8?Q?m=C3=A4=C3=B6 m=C3=A4=C3=B6.zip?=\""), result);
         Assert.IsTrue(result.Contains("\"NAME\" \"=?UTF-8?Q?m=C3=A4=C3=B6 m=C3=A4=C3=B6.zip?=\""), result);
      }

      [Test]
      [Description("Issue 218, IMAP: Problem with file name containing non-latin chars (RFC 2184 compliance)")]
      public void TestBodyStructureWithNonLatinCharacterTest2()
      {
         string @messageText =
            "To: test@test.com\r\n" +
            "Content-Type: multipart/mixed;\r\n" +
            " boundary=\"------------000008080307000003010005\"\r\n" +
            "\r\n" +
            "This is a multi-part message in MIME format.\r\n" +
            "--------------000008080307000003010005\r\n" +
            "Content-Type: text/plain; charset=ISO-8859-1; format=flowed\r\n" +
            "Content-Transfer-Encoding: 7bit\r\n" +
            "\r\n" +
            "Test\r\n" +
            "\r\n" +
            "--------------000008080307000003010005\r\n" +
            "Content-Type: image/png;\r\n" +
            " name=\"=?ISO-8859-1?Q?=E9=2Epng?=\"\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "Content-Disposition: inline;\r\n" +
            " filename*=ISO-8859-1''%E9%2E%70%6E%67\r\n" +
            "\r\n" +
            "iVBORw0KGgoAAAANSUhEUgAAAqgAAAH4CAIAAAAJvIhhAAAAAXNSR0IArs4c6QAAAARnQU1B\r\n" +
            "AACxjwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAA\r\n" +
            "--------------000008080307000003010005--\r\n";

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSendRaw(account.Address, account.Address, messageText);

         CustomAsserts.AssertFolderMessageCount(account.IMAPFolders[0], 1);

         var oSimulator = new ImapClientSimulator();
         oSimulator.ConnectAndLogon(account.Address, "test");
         oSimulator.SelectFolder("INBOX");
         string result = oSimulator.Fetch("1 BODYSTRUCTURE");
         oSimulator.Disconnect();

         Assert.IsTrue(result.Contains("\"FILENAME\" \"=?ISO-8859-1?Q?=E9=2E=70=6E=67?=\""));
         Assert.IsTrue(result.Contains("\"NAME\" \"=?ISO-8859-1?Q?=E9=2E=70=6E=67?=\""));
      }

      [Test]
      [Description("Issue 218, IMAP: Problem with file name containing non-latin chars (RFC 2184 compliance)")]
      public void TestBodyStructureWithNonLatinCharacterTest3()
      {
         string @messageText =
            "To: test@test.com\r\n" +
            "Content-Type: multipart/mixed;\r\n" +
            " boundary=\"------------000008080307000003010005\"\r\n" +
            "\r\n" +
            "This is a multi-part message in MIME format.\r\n" +
            "--------------000008080307000003010005\r\n" +
            "Content-Type: text/plain; charset=ISO-8859-1; format=flowed\r\n" +
            "Content-Transfer-Encoding: 7bit\r\n" +
            "\r\n" +
            "Test\r\n" +
            "\r\n" +
            "--------------000008080307000003010005\r\n" +
            "Content-Type: image/png;\r\n" +
            " name=\"=?ISO-8859-1?Q?=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=2E=70=6E=67?=\"\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "Content-Disposition: inline;\r\n" +
            " filename*=ISO-8859-1''%F6%50%C4%C9%CD%C1%D6%60%F6%F6%E4%27%2E%70%6E%67\r\n" +
            "\r\n" +
            "iVBORw0KGgoAAAANSUhEUgAAAqgAAAH4CAIAAAAJvIhhAAAAAXNSR0IArs4c6QAAAARnQU1B\r\n" +
            "AACxjwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAA\r\n" +
            "--------------000008080307000003010005--\r\n";

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSendRaw(account.Address, account.Address, messageText);

         CustomAsserts.AssertFolderMessageCount(account.IMAPFolders[0], 1);

         var oSimulator = new ImapClientSimulator();
         oSimulator.ConnectAndLogon(account.Address, "test");
         oSimulator.SelectFolder("INBOX");
         string result = oSimulator.Fetch("1 BODYSTRUCTURE");
         oSimulator.Disconnect();

         Assert.IsTrue(
            result.Contains("\"FILENAME\" \"=?ISO-8859-1?Q?=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=2E=70=6E=67?=\""));
         Assert.IsTrue(
            result.Contains("\"NAME\" \"=?ISO-8859-1?Q?=F6=50=C4=C9=CD=C1=D6=60=F6=F6=E4=27=2E=70=6E=67?=\""));
      }

      [Test]
      public void TestCapability()
      {
         Settings settings = _settings;

         settings.IMAPIdleEnabled = true;
         settings.IMAPQuotaEnabled = true;
         settings.IMAPSortEnabled = true;

         var oSimulator = new ImapClientSimulator();
         oSimulator.Connect();

         string sCapabilities = oSimulator.GetCapabilities();

         if (sCapabilities.IndexOf(" IDLE") == -1 ||
             sCapabilities.IndexOf(" QUOTA") == -1 ||
             sCapabilities.IndexOf(" SORT") == -1)
         {
            throw new Exception("ERROR - Wrong IMAP CAPABILITY.");
         }

         settings.IMAPIdleEnabled = false;
         settings.IMAPQuotaEnabled = true;
         settings.IMAPSortEnabled = true;
         sCapabilities = oSimulator.GetCapabilities();

         if (sCapabilities.IndexOf(" IDLE") != -1 ||
             sCapabilities.IndexOf(" QUOTA") == -1 ||
             sCapabilities.IndexOf(" SORT") == -1)
         {
            throw new Exception("ERROR - Wrong IMAP CAPABILITY.");
         }

         settings.IMAPIdleEnabled = false;
         settings.IMAPQuotaEnabled = false;
         settings.IMAPSortEnabled = true;
         sCapabilities = oSimulator.GetCapabilities();

         if (sCapabilities.IndexOf(" IDLE") != -1 ||
             sCapabilities.IndexOf(" QUOTA") != -1 ||
             sCapabilities.IndexOf(" SORT") == -1)
         {
            throw new Exception("ERROR - Wrong IMAP CAPABILITY.");
         }

         settings.IMAPIdleEnabled = false;
         settings.IMAPQuotaEnabled = false;
         settings.IMAPSortEnabled = false;
         sCapabilities = oSimulator.GetCapabilities();

         if (sCapabilities.IndexOf(" IDLE") != -1 ||
             sCapabilities.IndexOf(" QUOTA") != -1 ||
             sCapabilities.IndexOf(" SORT") != -1)
         {
            throw new Exception("ERROR - Wrong IMAP CAPABILITY.");
         }

         settings.IMAPIdleEnabled = true;
         settings.IMAPQuotaEnabled = false;
         settings.IMAPSortEnabled = false;
         sCapabilities = oSimulator.GetCapabilities();

         if (sCapabilities.IndexOf(" IDLE") == -1 ||
             sCapabilities.IndexOf(" QUOTA") != -1 ||
             sCapabilities.IndexOf(" SORT") != -1)
         {
            throw new Exception("ERROR - Wrong IMAP CAPABILITY.");
         }

         settings.IMAPIdleEnabled = true;
         settings.IMAPQuotaEnabled = true;
         settings.IMAPSortEnabled = false;
         sCapabilities = oSimulator.GetCapabilities();

         if (sCapabilities.IndexOf(" IDLE") == -1 ||
             sCapabilities.IndexOf(" QUOTA") == -1 ||
             sCapabilities.IndexOf(" SORT") != -1)
         {
            throw new Exception("ERROR - Wrong IMAP CAPABILITY.");
         }

         settings.IMAPACLEnabled = true;

         sCapabilities = oSimulator.GetCapabilities();
         Assert.IsTrue(sCapabilities.Contains(" ACL"));

         settings.IMAPACLEnabled = false;

         sCapabilities = oSimulator.GetCapabilities();
         Assert.IsFalse(sCapabilities.Contains(" ACL"));
      }

      [Test]
      public void TestCheck()
      {
         SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "check@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("check@test.com", "test");
         Assert.IsTrue(oSimulator.CreateFolder("TestFolder"));
         Assert.IsTrue(oSimulator.CheckFolder("TestFolder"));
         oSimulator.Disconnect();
      }

      [Test]
      public void TestClose()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "close@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("close@test.com", "test");
         Assert.IsFalse(oSimulator.Close());
         Assert.IsTrue(oSimulator.CreateFolder("TestFolder.Sub1"));
         Assert.IsTrue(oSimulator.SelectFolder("TestFolder.Sub1"));
         Assert.IsTrue(oSimulator.Close());
         oSimulator.Disconnect();
      }

      [Test]
      [Description(
         "Make sure that the connection object is released after folder is selected, idle mode is switched on and connection is closed."
         )]
      public void TestConnectionObjectRelease()
      {
         LogHandler.DeleteCurrentDefaultLog();

         _settings.IMAPIdleEnabled = true;

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "idleaccount@test.com", "test");

         var simulator = new ImapClientSimulator();

         string data;

         string sWelcomeMessage = simulator.Connect();
         simulator.Logon(account.Address, "test");
         Assert.IsTrue(simulator.SelectFolder("INBOX"));
         Assert.IsTrue(simulator.StartIdle());
         Assert.IsTrue(simulator.EndIdle(true, out data));
         Assert.IsTrue(simulator.Logout());

         string logData = LogHandler.ReadCurrentDefaultLog();

         Assert.IsTrue(LogHandler.DefaultLogContains("Ending session"));
      }

      [Test]
      public void TestDelete()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "delete@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("delete@test.com", "test");
         Assert.IsFalse(oSimulator.DeleteFolder("DoesNotExist"));
         Assert.IsTrue(oSimulator.CreateFolder("DoesExist"));
         Assert.IsTrue(oSimulator.SelectFolder("DoesExist"));
         oSimulator.Close();
         Assert.IsTrue(oSimulator.DeleteFolder("DoesExist"));
         Assert.IsFalse(oSimulator.SelectFolder("DoesExist"));
      }

      [Test]
      [Description("Test that deleting an IMAP folder does not stop notifications from working. (5.0 Build 315 Bug)")]
      public void TestDeleteIMAPFolderNotifications()
      {
         _settings.IMAPIdleEnabled = true;

         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "idleaccount@test.com", "test");

         var imapClientSimulator = new ImapClientSimulator();
         var oSimulator2 = new ImapClientSimulator();
         imapClientSimulator.ConnectAndLogon(oAccount.Address, "test");
         oSimulator2.ConnectAndLogon(oAccount.Address, "test");

         imapClientSimulator.SelectFolder("Inbox");
         oSimulator2.CreateFolder("Mailbox");
         oSimulator2.DeleteFolder("Mailbox");

         SmtpClientSimulator.StaticSend("test@test.com", oAccount.Address, "Test", "test");

         Pop3ClientSimulator.AssertMessageCount(oAccount.Address, "test", 1);

         string noopResponse = imapClientSimulator.NOOP() + imapClientSimulator.NOOP();

         // confirm that the client is notified about this message even though another
         // folder has been dropped by another client.
         Assert.IsTrue(noopResponse.Contains(@"* 1 EXISTS"), noopResponse);
      }


      [Test]
      public void TestExamineNonexistantFolder()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "delete@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("delete@test.com", "test");
         string result = oSimulator.ExamineFolder("NonexistantFolder");

         Assert.IsTrue(result.Contains("BAD Folder could not be found."));
      }

      [Test]
      [Description("Issue 294, IMAP: Incomplete SELECT response")]
      public void TestExamineResponse()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "testselect@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "Test", "Test");
         Pop3ClientSimulator.AssertMessageCount(account.Address, "test", 1);

         var oSimulator = new ImapClientSimulator();
         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("testselect@test.com", "test");
         string result = oSimulator.ExamineFolder("Inbox");
         oSimulator.Disconnect();

         Assert.IsTrue(result.Contains("* FLAGS"), result);
         Assert.IsTrue(result.Contains("* 1 EXISTS"), result);
         Assert.IsTrue(result.Contains("* 1 RECENT"), result);
         Assert.IsTrue(result.Contains("* OK [UNSEEN 1]"), result);
         Assert.IsTrue(result.Contains("* OK [PERMANENTFLAGS"), result);
         Assert.IsTrue(result.Contains("* OK [UIDNEXT 2]"), result);
         Assert.IsTrue(result.Contains("* OK [UIDVALIDITY"), result);
         Assert.IsTrue(result.Contains("OK [READ-ONLY]"), result);
      }

      [Test]
      [Description("Assert that the EXPUNGE command works")]
      public void TestExpunge()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "ExpungeAccount@test.com",
                                                                            "test");

         for (int i = 0; i < 3; i++)
            SmtpClientSimulator.StaticSend("test@test.com", account.Address, "Test", "test");

         Pop3ClientSimulator.AssertMessageCount(account.Address, "test", 3);

         var simulator = new ImapClientSimulator();
         Assert.IsTrue(simulator.ConnectAndLogon(account.Address, "test"));
         Assert.IsTrue(simulator.SelectFolder("Inbox"));

         Assert.IsTrue(simulator.SetFlagOnMessage(1, true, @"\Deleted"));
         Assert.IsTrue(simulator.SetFlagOnMessage(3, true, @"\Deleted"));

         string result;
         Assert.IsTrue(simulator.Expunge(out result));

         // Messages 1 and 2 should be deleted. 2, because when the first message
         // is deleted, the index of the message which was originally 3, is now 2.
         Assert.IsTrue(result.Contains("* 1 EXPUNGE\r\n* 2 EXPUNGE"));
      }

      [Test]
      [Description("Assert that when one client deletes a message, others are notified - even if IDLE isn't used.")]
      public void TestExpungeNotification()
      {
         _settings.IMAPIdleEnabled = true;

         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "idleaccount@test.com", "test");

         for (int i = 0; i < 5; i++)
            SmtpClientSimulator.StaticSend("test@test.com", oAccount.Address, "Test", "test");

         Pop3ClientSimulator.AssertMessageCount(oAccount.Address, "test", 5);

         var imapClientSimulator = new ImapClientSimulator();
         var oSimulator2 = new ImapClientSimulator();
         imapClientSimulator.ConnectAndLogon(oAccount.Address, "test");
         oSimulator2.ConnectAndLogon(oAccount.Address, "test");

         imapClientSimulator.SelectFolder("Inbox");
         oSimulator2.SelectFolder("Inbox");

         for (int i = 1; i <= 5; i++)
         {
            Assert.IsTrue(imapClientSimulator.SetFlagOnMessage(i, true, @"\Deleted"));
         }

         string noopResponse = oSimulator2.NOOP() + oSimulator2.NOOP();

         Assert.IsTrue(noopResponse.Contains(@"* 1 FETCH (FLAGS (\Deleted)") &&
                       noopResponse.Contains(@"* 1 FETCH (FLAGS (\Deleted)") &&
                       noopResponse.Contains(@"* 1 FETCH (FLAGS (\Deleted)") &&
                       noopResponse.Contains(@"* 1 FETCH (FLAGS (\Deleted)") &&
                       noopResponse.Contains(@"* 1 FETCH (FLAGS (\Deleted)"), noopResponse);

         bool result = imapClientSimulator.Expunge();

         string expungeResult = oSimulator2.NOOP() + oSimulator2.NOOP();

         Assert.IsTrue(
            expungeResult.Contains("* 1 EXPUNGE\r\n* 1 EXPUNGE\r\n* 1 EXPUNGE\r\n* 1 EXPUNGE\r\n* 1 EXPUNGE"),
            expungeResult);
      }

      [Test]
      public void TestFolderExpungeNotification()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "shared@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "TestSubject", "TestBody");
         ImapClientSimulator.AssertMessageCount(account.Address, "test", "Inbox", 1);

         var simulator1 = new ImapClientSimulator();
         var simulator2 = new ImapClientSimulator();

         simulator1.ConnectAndLogon(account.Address, "test");
         simulator2.ConnectAndLogon(account.Address, "test");

         simulator1.SelectFolder("Inbox");
         simulator2.SelectFolder("Inbox");

         string result = simulator2.NOOP();
         Assert.IsFalse(result.Contains("Deleted"));
         Assert.IsFalse(result.Contains("Seen"));

         simulator1.SetDeletedFlag(1);
         simulator1.Expunge();

         // the result may (should) come after the first NOOP response stream so do noop twice.
         result = simulator2.NOOP() + simulator2.NOOP();
         Assert.IsTrue(result.Contains("* 1 EXPUNGE"));

         simulator1.Disconnect();
         simulator2.Disconnect();
      }

      [Test]
      public void TestFolderNamesWithUnicodeChars()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.LogonWithLiteral("folder@test.com", "test");
         oSimulator.Send("A50 CREATE &AMQAxADEAMQAxADEAMQAxADEAMQAxADEAMQ-\r\n");
         oSimulator.Disconnect();

         string s = oAccount.IMAPFolders[1].Name;
         Assert.AreEqual("ÄÄÄÄÄÄÄÄÄÄÄÄÄ", s);
      }

      [Test]
      public void TestFolderUpdateNotification()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "shared@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "TestSubject", "TestBody");

         ImapClientSimulator.AssertMessageCount(account.Address, "test", "Inbox", 1);


         var simulator1 = new ImapClientSimulator();
         var simulator2 = new ImapClientSimulator();

         simulator1.ConnectAndLogon(account.Address, "test");
         simulator2.ConnectAndLogon(account.Address, "test");

         simulator1.SelectFolder("Inbox");
         simulator2.SelectFolder("Inbox");

         string result = simulator2.NOOP() + simulator2.NOOP();
         Assert.IsFalse(result.Contains("Deleted"));
         Assert.IsFalse(result.Contains("Seen"));

         simulator1.SetDeletedFlag(1);
         simulator1.SetSeenFlag(1);

         result = simulator2.NOOP() + simulator2.NOOP();
         Assert.IsTrue(result.Contains("Deleted"));
         Assert.IsTrue(result.Contains("Seen"));

         simulator1.Disconnect();
         simulator2.Disconnect();
      }

      [Test]
      public void TestGetQuota()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "imapaccount@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("imapaccount@test.com", "test");
         string result = oSimulator.GetQuota("Inbox");
         Assert.IsTrue(result.Contains("A09 OK"));
         oSimulator.Disconnect();
      }

      [Test]
      public void TestIdle()
      {
         _settings.IMAPIdleEnabled = true;

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "idleaccount@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon(account.Address, "test");
         Assert.IsTrue(oSimulator.SelectFolder("INBOX"));

         oSimulator.StartIdle();

         if (oSimulator.GetPendingDataExists())
            throw new Exception("Unexpected data exists");

         // Send a message to this account.
         var smtpClientSimulator = new SmtpClientSimulator();
         smtpClientSimulator.Send(account.Address, account.Address, "IDLE Test", "This is a test of IDLE");

         string data;
         Assert.IsTrue(oSimulator.EndIdle(false, out data));
      }

      [Test]
      public void TestLSUB()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "list@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.LogonWithLiteral("list@test.com", "test");
         string result = oSimulator.List().Substring(0, 1);
         Assert.AreEqual("*", result);
      }

      [Test]
      public void TestList()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "list@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("list@test.com", "test");
         string result = oSimulator.Send("A01 LIST \"\" \"*\"\r\n").Substring(0, 1);
         Assert.AreEqual("*", result);
      }

      [Test]
      public void TestLiteralSupport()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "literal@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.LogonWithLiteral("literal@test.com", "test");
         oSimulator.Disconnect();
      }

      [Test]
      public void TestLiterals()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("folder@test.com", "test");
         string result = oSimulator.Send("A01 CREATE {4}");
         result = oSimulator.Send("HEJS");

         oSimulator.Disconnect();
      }


      [Test]
      public void TestLogin()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "imapaccount@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("imapaccount@test.com", "test");
         oSimulator.Disconnect();
      }

      [Test]
      public void TestNamespace()
      {
         string imapFolderName = _settings.IMAPPublicFolderName;

         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "delete@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("delete@test.com", "test");
         string result = oSimulator.Send("A01 NAMESPACE");

         string correctNamespaceSetting = "* NAMESPACE ((\"\" \".\")) NIL ((\"" + imapFolderName + "\" \".\"))";

         if (!result.Contains(correctNamespaceSetting))
         {
            Assert.Fail("Namespace failed");
         }
      }


      /// <summary>
      /// Test that when you delete a message using POP3, IMAP notifications are sent.
      /// </summary>
      [Test]
      public void TestNotificationOnPOP3Deletion()
      {
         _settings.IMAPIdleEnabled = true;

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "idleaccount@test.com", "test");
         SmtpClientSimulator.StaticSend(account.Address, account.Address, "Message 1", "Body 1");
         SmtpClientSimulator.StaticSend(account.Address, account.Address, "Message 1", "Body 1");
         Pop3ClientSimulator.AssertMessageCount(account.Address, "test", 2);

         var imapSimulator = new ImapClientSimulator();
         string sWelcomeMessage = imapSimulator.Connect();
         Assert.IsTrue(imapSimulator.Logon("idleaccount@test.com", "test"));
         Assert.IsTrue(imapSimulator.SelectFolder("INBOX"));
         Assert.IsTrue(imapSimulator.StartIdle());

         var sim = new Pop3ClientSimulator();
         Assert.IsTrue(sim.ConnectAndLogon(account.Address, "test"));
         Assert.IsTrue(sim.DELE(1));
         sim.QUIT();

         // After a delete, the following should be sent tot he IMAP client:
         //  - EXPUNGE
         //  - EXISTS
         //  - RECENT
         Assert.IsTrue(imapSimulator.AssertPendingDataExists(), "No pending data exist");

         var deadline = DateTime.Now.AddSeconds(10);
         var message = new StringBuilder();

         while (DateTime.Now < deadline)
         {
            if (imapSimulator.GetPendingDataExists())
               message.Append(imapSimulator.Receive());

            var str = message.ToString();

            if (str.Contains("* 1 EXPUNGE") &&
                str.Contains("EXISTS") &&
                str.Contains("RECENT"))
            {
               return;
            }
         }

         var receivedText = message.ToString();
         Assert.IsTrue(receivedText.Contains("* 1 EXPUNGE"), receivedText);
         Assert.IsTrue(receivedText.Contains("EXISTS"), receivedText);
         Assert.IsTrue(receivedText.Contains("RECENT"), receivedText);
      }


      [Test]
      public void TestOutOfBounds()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "outofbounds@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("outofbounds@test.com", "test");

         string s = oSimulator.Send("A01 RENAME TEST");
         if (s.StartsWith("A01 BAD") == false)
            throw new Exception("ERROR - Out of bounds test failed");
      }

      [Test]
      public void TestPublicFolderUpdateNotification()
      {
         IMAPFolders folders = _application.Settings.PublicFolders;
         IMAPFolder folder = folders.Add("Share");
         folder.Save();

         IMAPFolderPermission permission = folder.Permissions.Add();
         permission.PermissionType = eACLPermissionType.ePermissionTypeAnyone;
         permission.set_Permission(eACLPermission.ePermissionLookup, true);
         permission.set_Permission(eACLPermission.ePermissionRead, true);
         permission.set_Permission(eACLPermission.ePermissionWriteOthers, true);
         permission.set_Permission(eACLPermission.ePermissionWriteSeen, true);
         permission.set_Permission(eACLPermission.ePermissionWriteDeleted, true);
         permission.set_Permission(eACLPermission.ePermissionInsert, true);
         permission.Save();

         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "shared@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "TestSubject", "TestBody");
         ImapClientSimulator.AssertMessageCount(account.Address, "test", "Inbox", 1);

         var simulator1 = new ImapClientSimulator();
         var simulator2 = new ImapClientSimulator();

         simulator1.ConnectAndLogon(account.Address, "test");
         simulator2.ConnectAndLogon(account.Address, "test");

         simulator1.SelectFolder("Inbox");
         simulator2.SelectFolder("Inbox");

         Assert.IsTrue(simulator1.Copy(1, "#Public.Share"));

         simulator1.SelectFolder("#Public.Share");
         simulator2.SelectFolder("#Public.Share");

         string result = simulator2.NOOP() + simulator2.NOOP();
         Assert.IsFalse(result.Contains("Deleted"));
         Assert.IsFalse(result.Contains("Seen"));

         simulator1.SetDeletedFlag(1);
         simulator1.SetSeenFlag(1);

         result = simulator2.NOOP() + simulator2.NOOP();
         Assert.IsTrue(result.Contains("Deleted"));
         Assert.IsTrue(result.Contains("Seen"));

         simulator1.Disconnect();
         simulator2.Disconnect();
      }

      [Test]
      [Description("Issue 244, Recent flag not removed properly.")]
      public void TestRecentRemovedOnMailboxChange()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "Test", "TestMessage");
         ImapClientSimulator.AssertMessageCount(account.Address, "test", "Inbox", 1);

         var sim = new ImapClientSimulator();
         Assert.IsTrue(sim.ConnectAndLogon(account.Address, "test"));
         Assert.IsTrue(sim.SelectFolder("Inbox"));
         Assert.IsTrue(sim.CreateFolder("Dummy"));
         Assert.IsTrue(sim.Copy(1, "Dummy"));
         string result = sim.SendSingleCommand("a01 select Dummy");
         Assert.IsTrue(result.Contains("* 1 EXISTS\r\n* 1 RECENT"), result);
         Assert.IsTrue(sim.SelectFolder("Inbox"));

         // Make sure that when we switched back to the Inbox, the Recent flag was removed.
         result = sim.SendSingleCommand("a01 select Dummy");
         Assert.IsFalse(result.Contains("* 1 EXISTS\r\n* 1 RECENT"), result);
         Assert.IsTrue(sim.Logout());
      }

      [Test]
      [Description("Issue 244, Recent flag not removed properly")]
      public void TestRecentRemovedOnMailboxClose()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "Test", "TestMessage");
         ImapClientSimulator.AssertMessageCount(account.Address, "test", "Inbox", 1);

         var sim = new ImapClientSimulator();
         Assert.IsTrue(sim.ConnectAndLogon(account.Address, "test"));
         Assert.IsTrue(sim.SelectFolder("Inbox"));
         Assert.IsTrue(sim.CreateFolder("Dummy"));
         Assert.IsTrue(sim.Copy(1, "Dummy"));
         string result = sim.SendSingleCommand("a01 select Dummy");
         Assert.IsTrue(result.Contains("* 1 EXISTS\r\n* 1 RECENT"), result);
         Assert.IsTrue(sim.Logout());

         sim = new ImapClientSimulator();
         Assert.IsTrue(sim.ConnectAndLogon(account.Address, "test"));
         result = sim.SendSingleCommand("a01 select Dummy");
         Assert.IsFalse(result.Contains("* 1 EXISTS\r\n* 1 RECENT"), result);
         Assert.IsTrue(sim.Logout());
      }

      [Test]
      public void TestRename()
      {
         Application application = SingletonProvider<TestSetup>.Instance.GetApp();
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("folder@test.com", "test");

         Assert.IsTrue(oSimulator.CreateFolder("Root1"));
         Assert.IsTrue(oSimulator.CreateFolder("Root1.Sub1"));
         Assert.IsTrue(oSimulator.CreateFolder("Root1.Sub2"));
         Assert.IsTrue(oSimulator.CreateFolder("Root1.Sub3"));
         Assert.IsTrue(oSimulator.SelectFolder("Root1"));
         Assert.IsTrue(oSimulator.SelectFolder("Root1.Sub1"));
         Assert.IsTrue(oSimulator.SelectFolder("Root1.Sub2"));
         Assert.IsTrue(oSimulator.SelectFolder("Root1.Sub3"));
         Assert.IsTrue(oSimulator.RenameFolder("Root1", "Root2"));
         Assert.IsTrue(oSimulator.SelectFolder("Root2"));
         Assert.IsTrue(oSimulator.SelectFolder("Root2.Sub1"));
         Assert.IsTrue(oSimulator.SelectFolder("Root2.Sub2"));
         Assert.IsTrue(oSimulator.SelectFolder("Root2.Sub3"));
         oSimulator.Disconnect();
      }

      [Test]
      public void TestRenameAndList()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("folder@test.com", "test");
         Assert.IsTrue(oSimulator.CreateFolder("Root1"));
         Assert.IsTrue(oSimulator.CreateFolder("Root2"));
         Assert.IsTrue(oSimulator.RenameFolder("Root2", "Root1.Root2"));

         string result = oSimulator.List();

         Assert.IsTrue(result.Contains("Root1.Root2"));

         oSimulator.Disconnect();
      }


      [Test]
      public void TestRenameAndList2()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("folder@test.com", "test");
         oSimulator.CreateFolder("Root1");
         oSimulator.CreateFolder("Root2");
         oSimulator.CreateFolder("Root3");

         oSimulator.RenameFolder("Root2", "Root1.Root2");
         oSimulator.RenameFolder("Root3", "Root1.Root2.Root3");

         string result = oSimulator.List();

         Assert.IsTrue(result.Contains("Root1\"\r\n"));
         Assert.IsTrue(result.Contains("Root1.Root2\"\r\n"));
         Assert.IsTrue(result.Contains("Root1.Root2.Root3\"\r\n"));

         oSimulator.Disconnect();
      }

      [Test]
      public void TestRenameAndList3()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("folder@test.com", "test");
         oSimulator.CreateFolder("Root1");
         oSimulator.CreateFolder("Root2");
         oSimulator.CreateFolder("Root3");

         oSimulator.RenameFolder("Root2", "Root1.Root2");
         oSimulator.RenameFolder("Root3", "Root1.Root2.Root3");
         oSimulator.RenameFolder("Root1.Root2.Root3", "Test");

         string result = oSimulator.List();

         Assert.IsTrue(result.Contains("Root1\"\r\n"));
         Assert.IsTrue(result.Contains("Root1.Root2\"\r\n"));
         Assert.IsTrue(result.Contains("Test\"\r\n"));

         oSimulator.Disconnect();
      }

      [Test]
      public void TestRenameAndList4()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("folder@test.com", "test");
         oSimulator.CreateFolder("Root1");
         oSimulator.CreateFolder("Root2.Root3");

         oSimulator.RenameFolder("Root2.Root3", "Root2.Root4");

         string result = oSimulator.List();

         Assert.IsTrue(result.Contains("Root1\"\r\n"));
         Assert.IsTrue(result.Contains("Root2.Root4\"\r\n"));

         oSimulator.Disconnect();
      }

      [Test]
      public void TestRenameAndList5()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "folder@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("folder@test.com", "test");
         oSimulator.CreateFolder("Root1");
         oSimulator.CreateFolder("Root2.Root3");

         oSimulator.RenameFolder("Root2.Root3", "Root2.Root4");
         oSimulator.RenameFolder("Root1", "Root2.Root4.Root1");

         string result = oSimulator.List();

         Assert.IsFalse(result.Contains(" Root1\r\n"));
         Assert.IsTrue(result.Contains("Root2.Root4.Root1\"\r\n"));

         Assert.IsFalse(oSimulator.SelectFolder("Root1"));
         Assert.IsTrue(oSimulator.SelectFolder("Root2.Root4.Root1"));

         oSimulator.Disconnect();
      }

      [Test]
      [Description("Test that renaming an IMAP folder does not stop notifications from working.")]
      public void TestRenameIMAPFolderNotifications()
      {
         _settings.IMAPIdleEnabled = true;

         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "idleaccount@test.com", "test");

         var imapClientSimulator = new ImapClientSimulator();
         var oSimulator2 = new ImapClientSimulator();
         imapClientSimulator.ConnectAndLogon(oAccount.Address, "test");
         oSimulator2.ConnectAndLogon(oAccount.Address, "test");

         imapClientSimulator.SelectFolder("Inbox");
         oSimulator2.CreateFolder("Mailbox");
         oSimulator2.RenameFolder("Mailbox", "Mailbox2");

         SmtpClientSimulator.StaticSend("test@test.com", oAccount.Address, "Test", "test");

         Pop3ClientSimulator.AssertMessageCount(oAccount.Address, "test", 1);

         string noopResponse = imapClientSimulator.NOOP() + imapClientSimulator.NOOP();

         // confirm that the client is notified about this message even though another
         // folder has been dropped by another client.
         Assert.IsTrue(noopResponse.Contains(@"* 1 EXISTS"), noopResponse);
      }

      [Test]
      [Description("Issue 294, IMAP: Incomplete SELECT response")]
      public void TestSelectResponse()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "testselect@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "Test", "Test");
         Pop3ClientSimulator.AssertMessageCount(account.Address, "test", 1);

         var oSimulator = new ImapClientSimulator();
         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("testselect@test.com", "test");
         string result = string.Empty;
         oSimulator.SelectFolder("Inbox", out result);
         oSimulator.Disconnect();

         Assert.IsTrue(result.Contains("* FLAGS"), result);
         Assert.IsTrue(result.Contains("* 1 EXISTS"), result);
         Assert.IsTrue(result.Contains("* 1 RECENT"), result);
         Assert.IsTrue(result.Contains("* OK [UNSEEN 1]"), result);
         Assert.IsTrue(result.Contains("* OK [PERMANENTFLAGS"), result);
         Assert.IsTrue(result.Contains("* OK [UIDNEXT 2]"), result);
         Assert.IsTrue(result.Contains("* OK [UIDVALIDITY"), result);
         Assert.IsTrue(result.Contains("OK [READ-WRITE]"), result);
      }

      [Test]
      public void TestStatus()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "imapaccount@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("imapaccount@test.com", "test");
         Assert.IsTrue(oSimulator.Status("Inbox", "MESSAGES").Contains("A08 OK"));
         oSimulator.Disconnect();
      }

      [Test]
      public void TestSubscribe()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "delete@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("delete@test.com", "test");
         Assert.IsTrue(oSimulator.CreateFolder("TestFolder1"));
         Assert.IsTrue(oSimulator.CreateFolder("TestFolder2"));
         Assert.IsTrue(oSimulator.CreateFolder("TestFolder3"));

         if (oSimulator.Subscribe("Vaffe"))
            Assert.Fail("Subscribe on non-existent folder succeeded");

         if (!oSimulator.Subscribe("TestFolder1"))
            Assert.Fail("Subscribe on existent folder failed");
         if (!oSimulator.Subscribe("TestFolder2"))
            Assert.Fail("Subscribe on existent folder failed");
         if (!oSimulator.Subscribe("TestFolder3"))
            Assert.Fail("Subscribe on existent folder failed");
      }


      [Test]
      [Description("Test that the SELECT response gives the correct unseen count.")]
      public void TestUnseenResponseInSelect()
      {
         Account account = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "test@test.com", "test");

         SmtpClientSimulator.StaticSend(account.Address, account.Address, "Test", "TestMessage");

         ImapClientSimulator.AssertMessageCount(account.Address, "test", "Inbox", 1);

         var sim = new ImapClientSimulator();
         Assert.IsTrue(sim.ConnectAndLogon(account.Address, "test"));
         Assert.IsTrue(sim.SelectFolder("Inbox"));
         Assert.IsTrue(sim.CreateFolder("Dummy"));
         Assert.IsTrue(sim.Copy(1, "Dummy"));

         string result = sim.SendSingleCommand("a01 select Dummy");
         Assert.IsTrue(result.Contains("* 1 EXISTS\r\n* 1 RECENT"), result);

         string searchResponse = sim.SendSingleCommand("srch1 SEARCH ALL UNSEEN");

         // We should have at least one message here.
         Assert.IsTrue(searchResponse.Contains("* SEARCH 1\r\n"), searchResponse);

         // Now fetch the body.
         string bodyText = sim.Fetch("1 BODY[TEXT]");

         // Now the message is no longer unseen. Confirm this.
         searchResponse = sim.SendSingleCommand("srch1 SEARCH ALL UNSEEN");
         Assert.IsTrue(searchResponse.Contains("* SEARCH\r\n"), searchResponse);

         // Close the messages to mark them as no longer recent.
         Assert.IsTrue(sim.Close());

         result = sim.SendSingleCommand("a01 select Dummy");
         Assert.IsTrue(result.Contains("* 1 EXISTS\r\n* 0 RECENT"), result);
      }

      [Test]
      public void TestUnsubscribe()
      {
         Account oAccount = SingletonProvider<TestSetup>.Instance.AddAccount(_domain, "delete@test.com", "test");

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();
         oSimulator.Logon("delete@test.com", "test");
         Assert.IsTrue(oSimulator.CreateFolder("TestFolder1"));
         Assert.IsTrue(oSimulator.CreateFolder("TestFolder2"));

         if (!oSimulator.Subscribe("TestFolder1"))
            Assert.Fail("Subscribe on existent folder failed");
         if (!oSimulator.Subscribe("TestFolder2"))
            Assert.Fail("Subscribe on existent folder failed");

         if (!oSimulator.Unsubscribe("TestFolder1"))
            Assert.Fail("Unsubscribe on existent folder failed");
         if (!oSimulator.Unsubscribe("TestFolder2"))
            Assert.Fail("Unsubscribe on existent folder failed");
      }

      [Test]
      public void TestWelcomeMessage()
      {
         _settings.WelcomeIMAP = "HOWDYHO IMAP";

         var oSimulator = new ImapClientSimulator();

         string sWelcomeMessage = oSimulator.Connect();

         oSimulator.Disconnect();

         if (sWelcomeMessage != "* OK HOWDYHO IMAP\r\n")
            throw new Exception("ERROR - Wrong welcome message.");
      }
   }
}