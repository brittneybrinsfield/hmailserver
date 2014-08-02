// Copyright (c) 2010 Martin Knafve / hMailServer.com.  
// http://www.hmailserver.com

#include "stdafx.h"
#include "MessageData.h"

#include "../Mime/Mime.h"
#include "../Mime/MimeCode.h"
#include "../BO/Message.h"
#include "../BO/Attachments.h"
#include "../Util/Time.h"
#include "../Util/GUIDCreator.h"
#include "../Util/Utilities.h"

// Test
#include "../Persistence/PersistentMessage.h"
#include "../../SMTP/RecipientParser.h"

#define XHMAILSERVER_LOOPCOUNT _T("X-hMailServer-LoopCount")

#ifdef _DEBUG
#define DEBUG_NEW new(_NORMAL_BLOCK, __FILE__, __LINE__)
#define new DEBUG_NEW
#endif

#ifdef _DEBUG
#define DEBUG_NEW new(_NORMAL_BLOCK, __FILE__, __LINE__)
#define new DEBUG_NEW
#endif

namespace HM
{
   MessageData::MessageData()
   {
      m_bEncodeFields = true;
      _unfoldWithSpace = true;

      mime_mail_ = shared_ptr<MimeBody>(new MimeBody);
   }

   bool
   MessageData::LoadFromMessage(shared_ptr<const Account> account, shared_ptr<Message> pMessage)
   {
      String fileName = PersistentMessage::GetFileName(account, pMessage);

      return LoadFromMessage(fileName, pMessage);
   }
   

   bool
   MessageData::LoadFromMessage(const String &fileName, shared_ptr<Message> pMessage)
   {
      message_ = pMessage;
      _messageFileName = fileName;

      mime_mail_ = shared_ptr<MimeBody>(new MimeBody);

      const int MaxSize = 1024*1024 * 80; // we'll ignore messages larger than 80MB.
      if (FileUtilities::FileSize(_messageFileName) > MaxSize)
         return false;

      bool bNewMessage = false;
      try
      {

         if (!mime_mail_->LoadFromFile(_messageFileName))
         {
            bNewMessage = true;
         }
      }
      catch (...)
      {
         try
         {
            String sFileNameExclPath = FileUtilities::GetFileNameFromFullPath(_messageFileName);

            String sMessageBackupPath = IniFileSettings::Instance()->GetLogDirectory() + "\\Problematic messages\\" + sFileNameExclPath;
            FileUtilities::Copy(_messageFileName, sMessageBackupPath, true);

            String sErrorMessage;
            sErrorMessage.Format(_T("An unknown error occurred while loading message. File: %s. Backuped to: %s"), _messageFileName, sMessageBackupPath); 

            ErrorManager::Instance()->ReportError(ErrorManager::Medium, 4218, "MessageData::LoadFromMessage", sErrorMessage);
         }
         catch (...)
         {
            ErrorManager::Instance()->ReportError(ErrorManager::Medium, 4218, "MessageData::LoadFromMessage", "An unknown error occurred while loading message.");
         }

         return false;
      }

      if (bNewMessage)
      {
         // For new messages, we default to UTF-8. This way client
         // can put any values into headers without having to care
         // about setting the correct character set first.
         SetCharset("utf-8");
         SetFieldValue(CMimeConst::MimeVersion(), "1.0");
      }

      return true;
   }


   bool 
   MessageData::RefreshFromMessage()
   {
      if (!message_)
         return false;

      return LoadFromMessage(_messageFileName, message_);
   }


   MessageData::~MessageData()
   {

   }

   void 
   MessageData::DeleteField(const AnsiString &headerName)
   {
      MimeField *pField = mime_mail_->GetField(headerName);
      
      while (pField)
      {
         mime_mail_->DeleteField(pField);
         pField = mime_mail_->GetField(headerName);
      }
   }

   String
   MessageData::GetSubject()  const
   {
      return GetFieldValue("Subject");
   }

   void 
   MessageData::SetSubject(const String &sSubject)
   {
      SetFieldValue("Subject", sSubject);
   }

   String
   MessageData::GetCharset()  const
   {
      return mime_mail_->GetCharset();
   }

   void 
   MessageData::SetCharset(const String &sCharset)
   {
      AnsiString sCharsetStr = sCharset;
      mime_mail_->SetCharset(sCharsetStr);
   }

   String
   MessageData::GetHeader()  const
   {
      if (m_bEncodeFields)
         return mime_mail_->GetUnicodeHeaderContents();
      else
         return mime_mail_->GetHeaderContents();
   }

   String
   MessageData::GetSentTime()  const
   {
      return GetFieldValue("Date");
   }   

   void
   MessageData::SetSentTime(const String &sSentTime)
   {
      String sValue = sSentTime;
      if (sValue.IsEmpty())
      {
         // Use default value
         sValue = Time::GetCurrentMimeDate();
      }

      SetFieldValue("Date", sValue);
   }

   void 
   MessageData::SetFieldValue(const String &sField, const String &sValue)
   {
      if (m_bEncodeFields)
         mime_mail_->SetUnicodeFieldValue(sField, sValue, "");
      else
         mime_mail_->SetRawFieldValue(sField, sValue, "");
   }

   String
   MessageData::GetTo() const
   {
      return GetFieldValue("To");
   } 

   String
   MessageData::GetReturnPath() const
   {
      return GetFieldValue("Return-Path");
   } 

   void 
   MessageData::SetReturnPath(const String &sReturnPath)
   {
      SetFieldValue("Return-Path", "<" + sReturnPath + ">");
   }

   String
   MessageData::GetCC() const
   {
      return GetFieldValue("CC");
   } 

   void 
   MessageData::SetCC(const String &sCC)
   {
      SetFieldValue("CC", sCC);
   }

   String
   MessageData::GetBCC() const
   {
      return GetFieldValue("BCC");
   } 

   void 
   MessageData::SetBCC(const String &sBCC)
   {
      SetFieldValue("BCC", sBCC);
   }

   String
   MessageData::GetFrom() const
   {
      return GetFieldValue("From");
   } 

   void 
   MessageData::SetFrom(const String &sFrom)
   {
      SetFieldValue("From", sFrom);
   }

   void 
   MessageData::SetTo(const String &sTo)
   {
      SetFieldValue("To", sTo);
   }


   String
   MessageData::GetFieldValue(const String &sName) const
   {
      String sRetVal;
      if (m_bEncodeFields)
         sRetVal = mime_mail_->GetUnicodeFieldValue(sName);
      else
         sRetVal = mime_mail_->GetRawFieldValue(sName);

      return sRetVal;
   }

   int 
   MessageData::GetSize() const
   {
      return message_->GetSize();
   }

   shared_ptr<Attachments>
   MessageData::GetAttachments()
   {
      if (!attachments_)
      {
         attachments_ = shared_ptr<Attachments>(new Attachments(mime_mail_, this));
         
         // Load attachments.
         attachments_->Load();
      }

      return attachments_;
   }

   String 
   MessageData::GetBody() const
   {
      shared_ptr<MimeBody> pPart = FindPart("text/plain");

      if (pPart)
      {
         if (m_bEncodeFields)
            return pPart->GetUnicodeText();
         else
            return pPart->GetRawText();
      }
      else
         return "";
    
   }

   void
   MessageData::SetBody(const String &sBody)
   {
      shared_ptr<MimeBody> pPart = FindPart("text/plain");

      if (!pPart)
         pPart = CreatePart("text/plain");

      String sModifiedBody = sBody;
      if (sModifiedBody.Right(2) != _T("\r\n"))
      {
         // Add carriage return.
         sModifiedBody += "\r\n";
      }

      // Set the text to the part
      if (m_bEncodeFields)
         pPart->SetUnicodeText(sModifiedBody);
      else
         pPart->SetRawText(sModifiedBody);
   }

   String 
   MessageData::GetHTMLBody() const
   {
      shared_ptr<MimeBody> pPart = FindPart("text/html");

      if (pPart)
      {
         if (m_bEncodeFields)
            return pPart->GetUnicodeText();
         else
            return pPart->GetRawText();
      }

      return "";
   }

   void
   MessageData::SetHTMLBody(const String &sNewVal)
   {
      shared_ptr<MimeBody> pHTMLPart = FindPart("text/html");

      if (!pHTMLPart)
      {
         // Create a new part.
         pHTMLPart = CreatePart("text/html");
      }

      String sModifiedBody = sNewVal;
      if (sModifiedBody.Right(2) != _T("\r\n"))
      {
         // Add carriage return.
         sModifiedBody += "\r\n";
      }

      // Set the text to the part
      if (m_bEncodeFields)
         pHTMLPart->SetUnicodeText(sModifiedBody);
      else
         pHTMLPart->SetRawText(sModifiedBody);
   }

   shared_ptr<MimeBody> 
   MessageData::FindPartNoRecurse(shared_ptr<MimeBody> parent, const AnsiString &sType) const
   {
      shared_ptr<MimeBody> pPart = parent->FindFirstPart();

      while (pPart)
      {
         AnsiString sContentType = pPart->GetCleanContentType();
         sContentType.MakeLower();

         if (sContentType.CompareNoCase(sType) == 0)
         {
            // Create a new part in the end of the message. We have already
            // looked for a part with the proper type without success. This
            // is probably a new attachment.
            return pPart;
         }

         pPart = parent->FindNextPart();
      }
      
      shared_ptr<MimeBody> empty;
      return empty;
   }

   shared_ptr<MimeBody> 
   MessageData::CreatePart(const String &sContentType)
   {
      // Step 1: Extract all parts.
      // Step 2: Delete everything
      // Step 3: Create the new type.
      // Step 4: Insert the new type and all others.

      // Create a new part by rebuilding the message more or less from scratch.
      AnsiString sMainBodyType = mime_mail_->GetCleanContentType();
      AnsiString sMainBodyCharset = mime_mail_->GetCharset();
      sMainBodyType.MakeLower();
      
      shared_ptr<MimeBody> textPart = FindPartNoRecurse(mime_mail_, "text/plain");
      shared_ptr<MimeBody> htmlPart = FindPartNoRecurse(mime_mail_, "text/html");

      shared_ptr<MimeBody> retValue;

      shared_ptr<MimeBody> alternativeNode = FindPartNoRecurse(mime_mail_, "multipart/alternative");
      if (alternativeNode)
      {
         if (!textPart) 
         {
            textPart = FindPartNoRecurse(alternativeNode, "text/plain");
            if (textPart)
               alternativeNode->ErasePart(textPart);
         }

         if (!htmlPart)
         {
            htmlPart = FindPartNoRecurse(alternativeNode, "text/html");

            if (htmlPart)
               alternativeNode->ErasePart(htmlPart);
         }

         mime_mail_->ErasePart(alternativeNode);
      }

      if (!textPart && !htmlPart)
      {
         // We don't have any text or HMTL part. Copy the main content
         // of the message to a new part, if the main content isn't empty.
         if (sMainBodyType == "" || sMainBodyType == "text/plain")
         {
            if (mime_mail_->GetRawText().size() > 0)
            {
               textPart = shared_ptr<MimeBody>(new MimeBody);
               textPart->SetRawText(mime_mail_->GetRawText());
               textPart->SetContentType("text/plain", "");
               
               if (!sMainBodyCharset.IsEmpty())
                  textPart->SetCharset(sMainBodyCharset);

               AnsiString originalTransferEncoding = mime_mail_->GetTransferEncoding();
               if (!originalTransferEncoding.IsEmpty())
                  textPart->SetTransferEncoding(originalTransferEncoding);
            }
         }
         else if (sMainBodyType == "text/html")
         {
            if (mime_mail_->GetRawText().size() > 0)
            {
               htmlPart = shared_ptr<MimeBody>(new MimeBody);
               htmlPart->SetRawText(mime_mail_->GetRawText());
               htmlPart->SetContentType("text/html", "");
               
               if (!sMainBodyCharset.IsEmpty())
                  htmlPart->SetCharset(sMainBodyCharset);

               AnsiString originalTransferEncoding = mime_mail_->GetTransferEncoding();
               if (!originalTransferEncoding.IsEmpty())
                  htmlPart->SetTransferEncoding(originalTransferEncoding);
            }
         }
      }

      // Locate the other parts which are not text or html.
      //
      // When we get here, any alternative, text or html parts
      // should have been removed from the message already.
      //
      shared_ptr<MimeBody> part = mime_mail_->FindFirstPart();
      set<shared_ptr<MimeBody> > setAttachments;
      while (part)
      {
         AnsiString subContentType = part->GetCleanContentType();
         if (!IsTextType(subContentType) && !IsHTMLType(subContentType))
            setAttachments.insert(part);

         part = mime_mail_->FindNextPart();
      }

      // Remove all parts so that we can rebuild it again.
      mime_mail_->DeleteAll();

      // Create the brand new part...
      if (sContentType.CompareNoCase(_T("text/plain")) == 0)
      {
         assert (textPart == 0);

         if (setAttachments.size() == 0 && !htmlPart)
         {
            // Reuse the main part. There's no need to add a new one.
            textPart = mime_mail_;
            textPart->SetContentType("text/plain", "");
         }
         else
         {
            textPart = shared_ptr<MimeBody>(new MimeBody);
            textPart->SetContentType("text/plain", "");

            AnsiString transferEncoding = mime_mail_->GetTransferEncoding();
            if (!transferEncoding.IsEmpty())
               textPart->SetTransferEncoding(transferEncoding);

            if (!sMainBodyCharset.IsEmpty())
               textPart->SetCharset(sMainBodyCharset);

         }

         retValue = textPart;
      }
      else if (sContentType.CompareNoCase(_T("text/html")) == 0)
      {
         assert (htmlPart == 0);

         if (setAttachments.size() == 0 && !textPart)
         {
            // Reuse the main part. There's no need to add a new one.

            htmlPart = mime_mail_;
            htmlPart->SetContentType("text/html", "");
         }
         else
         {
            htmlPart = shared_ptr<MimeBody>(new MimeBody);
            htmlPart->SetContentType("text/html", "");

            AnsiString transferEncoding = mime_mail_->GetTransferEncoding();
            if (!transferEncoding.IsEmpty())
               htmlPart->SetTransferEncoding(transferEncoding);

            if (!sMainBodyCharset.IsEmpty())
               htmlPart->SetCharset(sMainBodyCharset);
         }

         retValue = htmlPart;
         
      }
      else
      {
         // create a new item. treat as an attachment.
         retValue = shared_ptr<MimeBody>(new MimeBody);
         setAttachments.insert(retValue);
      }

      AnsiString mainBodyType;
      if (setAttachments.size() > 0)
         mainBodyType = "multipart/mixed";
      else if (textPart && htmlPart)
         mainBodyType = "multipart/alternative";
      else if (htmlPart)
         mainBodyType = "text/html";
      else
         mainBodyType = "text/plain";

      if (textPart && htmlPart)
      {
         if (mainBodyType == "multipart/mixed")
         {
            shared_ptr<MimeBody> alternativePart = shared_ptr<MimeBody>(new MimeBody);
            alternativePart->SetContentType("multipart/alternative", "");
            alternativePart->SetRawText("This is a multi-part message.\r\n\r\n");

            alternativePart->AddPart(textPart);
            alternativePart->AddPart(htmlPart);
            alternativePart->SetBoundary(NULL);

            mime_mail_->AddPart(alternativePart);
         }
         else
         {
            if (mime_mail_ != textPart)
               mime_mail_->AddPart(textPart);

            if (mime_mail_ != htmlPart)
               mime_mail_->AddPart(htmlPart);
         }
         
      }
      else if (textPart)
      {
         if (mainBodyType == "multipart/mixed")
         {
            if (mime_mail_ != textPart)
               mime_mail_->AddPart(textPart);
         }
      }
      else if (htmlPart)
      {
         if (mainBodyType == "multipart/mixed")
         {
            if (mime_mail_ != htmlPart)
               mime_mail_->AddPart(htmlPart);
         }
      }

      boost_foreach(shared_ptr<MimeBody> pAttachment, setAttachments)
      {
         mime_mail_->AddPart(pAttachment);
      }

      mime_mail_->SetContentType(mainBodyType, ""); 
      if (!sMainBodyCharset.IsEmpty())
         mime_mail_->SetCharset(sMainBodyCharset);

      if (mime_mail_->GetPartCount() > 0)
      {
         mime_mail_->DeleteField(CMimeConst::TransferEncoding());
         mime_mail_->SetRawText("This is a multi-part message.\r\n\r\n");
         mime_mail_->SetBoundary(NULL);
      }

      return retValue;
   }

   shared_ptr<MimeBody>
   MessageData::FindPart(const String &sType) const
   {
      String sPartType = mime_mail_->GetCleanContentType();

      if (sPartType.CompareNoCase(sType) == 0)
         return mime_mail_;

      shared_ptr<MimeBody> pPart = mime_mail_->FindFirstPart();

      if (!pPart)
      {
         shared_ptr<MimeBody> pEmpty;
         return pEmpty;
      }

      while (pPart)
      {
         String sContentType = pPart->GetCleanContentType();

         if (sContentType.CompareNoCase(sType) == 0)
            return pPart;
         
         if (pPart->IsMultiPart())
         {
            shared_ptr<MimeBody> pSubPart = pPart->FindFirstPart();

            while (pSubPart)
            {
               String sSubContentType = pSubPart->GetCleanContentType();
               
               if (sSubContentType.CompareNoCase(sType) == 0)
                  return pSubPart;

            
               pSubPart = pPart->FindNextPart();
            }

         }
         
         pPart = mime_mail_->FindNextPart();
      }

      shared_ptr<MimeBody> pEmpty;
      return pEmpty;     

   }

   bool 
   MessageData::Write(const String &fileName)
   {
      const HM::String directoryName = HM::FileUtilities::GetFilePath(fileName);
      if (!HM::FileUtilities::Exists(directoryName))
         HM::FileUtilities::CreateDirectoryRecursive(directoryName);

      bool result = mime_mail_->SaveAllToFile(fileName);

      if (message_)
      {
         message_->SetSize(FileUtilities::FileSize(fileName));
      }

      return result;
   }

   bool 
   MessageData::GetHasBodyType(const String &sBodyType)
   {
      shared_ptr<MimeBody> pPart = FindPart(sBodyType);      

      return pPart ? true : false;
   }

   int
   MessageData::GetRuleLoopCount()
   {
      String sRulesProcessed = mime_mail_->GetRawFieldValue(XHMAILSERVER_LOOPCOUNT);
      if (sRulesProcessed.IsEmpty())
         return 0;

      return _ttoi(sRulesProcessed);
   }

   void
   MessageData::IncreaseRuleLoopCount()
   {
      int iCurrentNo = GetRuleLoopCount() + 1;
      
      SetRuleLoopCount(iCurrentNo);
   }

   void 
   MessageData::SetRuleLoopCount(int iLoopCount)
   {
      mime_mail_->SetRawFieldValue(XHMAILSERVER_LOOPCOUNT, StringParser::IntToString(iLoopCount), "");
   }


   void 
   MessageData::GenerateMessageID()
   {
      String sGUID = GUIDCreator::GetGUID();
      sGUID.Replace(_T("{"), _T(""));
      sGUID.Replace(_T("}"), _T(""));
      
      String sMsgID;
      sMsgID.Format(_T("<%s@%s>"), sGUID , Utilities::ComputerName());

      SetFieldValue("Message-ID", sMsgID);
   }

   bool 
   MessageData::IsTextType(const String &sContentType)
   {
      return sContentType.CompareNoCase(_T("text/plain")) == 0;
   }

   bool 
   MessageData::IsHTMLType(const String &sContentType)
   {
      return sContentType.CompareNoCase(_T("text/html")) == 0;
   }

   shared_ptr<MimeBody> 
   MessageData::GetMimeMessage()
   {
      return mime_mail_;
   }

   void
   MessageData::SetAutoReplied()
   {
	   SetFieldValue("Auto-Submitted", "auto-replied");
   }

   bool
   MessageData::IsAutoSubmitted()
   {
		String autoSubmitted = GetFieldValue("Auto-Submitted");
		if (autoSubmitted.IsEmpty())
			return false;

		if (autoSubmitted.CompareNoCase(_T("no")) == 0)
			return false;

		return true;
   }

   void
   MessageDataTester::Test()
   {
      return;
      // Following tests are only made in DEBUG.
      #ifndef _DEBUG
         return;
      #endif

      shared_ptr<Message> pMessage = shared_ptr<Message>(new Message);

      // Add recipient
      bool recipientOK = false;
      RecipientParser recipientParser;
      recipientParser.CreateMessageRecipientList("test@test.com", pMessage->GetRecipients(), recipientOK);

      shared_ptr<Account> account;

      // Create message data structure
      shared_ptr<MessageData> pMsgData = shared_ptr<MessageData>(new MessageData());
      pMsgData->LoadFromMessage(account, pMessage);
      pMsgData->SetTo("test@test.com");
      pMsgData->SetFrom("test@test.com");
      pMsgData->SetSubject("Hejsan");
      pMsgData->SetFieldValue("MIME-Version", "1.0");
      
      // Date was not specified. Specify it now.
      String sDate = Time::GetCurrentMimeDate();
      pMsgData->SetSentTime(sDate);


      // Add an attachment.
      pMsgData->GetAttachments()->Add("C:\\windows\\notepad.exe");
      pMsgData->GetAttachments()->Add("C:\\windows\\system32\\calc.exe");

      // Set body contents
      pMsgData->SetHTMLBody("Min <b>HTML</b> body");
      pMsgData->SetHTMLBody("Min <b>HTML</b> body");
      pMsgData->SetBody("Min plaintext body");

      String fileName = PersistentMessage::GetFileName(account, pMessage);

      // Write it
      pMsgData->Write(fileName);
      

      // Save it
      PersistentMessage::SaveObject(pMessage);

      Application::Instance()->SubmitPendingEmail();

      Sleep(50000);
   }



}
