// Copyright (c) 2010 Martin Knafve / hMailServer.com.  
// http://www.hmailserver.com

#include "stdafx.h"
#include "IMAPCommandAppend.h"
#include "IMAPConnection.h"
#include "../Common/BO/IMAPFolders.h"
#include "../Common/BO/Message.h"
#include "../Common/BO/Account.h"
#include "../Common/BO/Domain.h"
#include "../Common/BO/IMAPFolder.h"
#include "../Common/Persistence/PersistentAccount.h"
#include "../Common/Persistence/PersistentMessage.h"
#include "../Common/Util/Time.h"
#include "../Common/Util/File.h"
#include "../Common/Util/ByteBuffer.h"
#include "../Common/Cache/CacheContainer.h"
#include "../Common/BO/ACLPermission.h"
#include "../Common/Tracking/ChangeNotification.h"
#include "../Common/Tracking/NotificationServer.h"
#include "../SMTP/SMTPConfiguration.h"

#include "IMAPSimpleCommandParser.h"

#ifdef _DEBUG
#define DEBUG_NEW new(_NORMAL_BLOCK, __FILE__, __LINE__)
#define new DEBUG_NEW
#endif

namespace HM
{

   IMAPCommandAppend::IMAPCommandAppend()
   {
   }

   IMAPCommandAppend::~IMAPCommandAppend()
   {
      _KillCurrentMessage();
   }

   void 
   IMAPCommandAppend::_KillCurrentMessage()
   {
      if (!current_message_)
         return;

      if (FileUtilities::Exists(m_sMessageFileName))
         FileUtilities::DeleteFile(m_sMessageFileName);
   }

   IMAPResult
   IMAPCommandAppend::ExecuteCommand(shared_ptr<IMAPConnection> pConnection, shared_ptr<IMAPCommandArgument> pArgument)
   {
      if (!pConnection->IsAuthenticated())
         return IMAPResult(IMAPResult::ResultNo, "Authenticate first");
      
      m_sCurrentTag = pArgument->Tag();
      
      // Reset these two so we don't re-use old values.
      m_sFlagsToSet = "";
      m_sCreateTimeToSet = "";

      shared_ptr<IMAPSimpleCommandParser> pParser = shared_ptr<IMAPSimpleCommandParser>(new IMAPSimpleCommandParser());

      pParser->Parse(pArgument);

      if (pParser->WordCount() < 3)
         return IMAPResult(IMAPResult::ResultBad, "APPEND Command requires at least 2 parameter.");
      
         // Create a new mailbox
      String sFolderName = pParser->GetParamValue(pArgument, 0);
      if (!pParser->Word(1)->Clammerized())
         IMAPFolder::UnescapeFolderString(sFolderName);
     
      if (pParser->ParantheziedWord())
         m_sFlagsToSet = pParser->ParantheziedWord()->Value();

      // last word.
      shared_ptr<IMAPSimpleWord> pWord = pParser->Word(pParser->WordCount()-1);

      if (!pWord || !pWord->Clammerized())
         return IMAPResult(IMAPResult::ResultBad, "Missing literal");

      AnsiString literalSize = pWord->Value();
       
      m_lBytesLeftToReceive = atoi(literalSize);
      if (m_lBytesLeftToReceive == 0)
         return IMAPResult(IMAPResult::ResultBad, "Empty message not permitted.");
      
      // Add an extra two bytes since we expect a <newline> in the end.
      m_lBytesLeftToReceive += 2;

      shared_ptr<const Domain> domain = CacheContainer::Instance()->GetDomain(pConnection->GetAccount()->GetDomainID());
      int maxMessageSizeKB = _GetMaxMessageSize(domain);

      if (maxMessageSizeKB > 0 && 
          m_lBytesLeftToReceive / 1024 > maxMessageSizeKB)
      {
         String sMessage;
         sMessage.Format(_T("Message size exceeds fixed maximum message size. Size: %d KB, Max size: %d KB"), 
            m_lBytesLeftToReceive / 1024, maxMessageSizeKB);

         return IMAPResult(IMAPResult::ResultNo, sMessage);
      }

      // Locate the parameter containing the date to set.
      // Can't use pParser->QuotedWord() since there may
      // be many quoted words in the command.
      
      for (int i = 2; i < pParser->WordCount(); i++)
      {
         shared_ptr<IMAPSimpleWord> pWord = pParser->Word(i);

         if (pWord->Quoted())
         {
            m_sCreateTimeToSet = pWord->Value();

            // date-day-fixed  = (SP DIGIT) / 2DIGIT
            //   ; Fixed-format version of date-day
            // If the date given starts with <space>number, we need
            // to Trim. Doesn't hurt to always do this.
            m_sCreateTimeToSet.TrimLeft();
         }
      }

      destination_folder_ = pConnection->GetFolderByFullPath(sFolderName);
      if (!destination_folder_)
         return IMAPResult(IMAPResult::ResultBad, "Folder could not be found.");

      if (!destination_folder_->IsPublicFolder())
      {
         // Make sure that this message fits in the mailbox.
         shared_ptr<const Account> pAccount = CacheContainer::Instance()->GetAccount(pConnection->GetAccount()->GetID());
         
         if (!pAccount)
            return IMAPResult(IMAPResult::ResultNo, "Account could not be fetched.");

         if (!pAccount->SpaceAvailable(m_lBytesLeftToReceive))
            return IMAPResult(IMAPResult::ResultNo, "Your quota has been exceeded.");
      }

      if (!pConnection->CheckPermission(destination_folder_, ACLPermission::PermissionInsert))
         return IMAPResult(IMAPResult::ResultBad, "ACL: Insert permission denied (Required for APPEND command).");



      __int64 lFolderID = destination_folder_->GetID();

      current_message_ = shared_ptr<Message>(new Message);
      current_message_->SetAccountID(destination_folder_->GetAccountID());
      current_message_->SetFolderID(lFolderID);

      // Construct a file name which we'll write the message to.
      // Should we connect this message to an account? Yes, if this is not a public folder.
      shared_ptr<const Account> pMessageOwner;
      if (!destination_folder_->IsPublicFolder())
         pMessageOwner = pConnection->GetAccount();

      m_sMessageFileName = PersistentMessage::GetFileName(pMessageOwner, current_message_);

      String sResponse = "+ Ready for literal data\r\n";
      pConnection->SetReceiveBinary(true);
      pConnection->SendAsciiData(sResponse);    

      return IMAPResult();
   }

   void
   IMAPCommandAppend::ParseBinary(shared_ptr<IMAPConnection> pConnection, shared_ptr<ByteBuffer> pBuf)
   {
      _appendBuffer.Add(pBuf);
   
      if (_appendBuffer.GetSize() >= m_lBytesLeftToReceive)
      {
         _WriteData(pConnection, _appendBuffer.GetBuffer(), _appendBuffer.GetSize());

         pConnection->SetReceiveBinary(false);
   
         _appendBuffer.Empty();

         _Finish(pConnection);

         pConnection->PostReceive();
      }
      else
      {
         _TruncateBuffer(pConnection);

         pConnection->PostBufferReceive();
      }

   }
   
   bool
   IMAPCommandAppend::_WriteData(const shared_ptr<IMAPConnection>  pConn, const BYTE *pBuf, int WriteLen)
   {
      if (!current_message_)
         return false;

      String destinationPath = FileUtilities::GetFilePath(m_sMessageFileName);
      if (!FileUtilities::Exists(destinationPath))
         FileUtilities::CreateDirectoryRecursive(destinationPath);

      File oFile;
      if (!oFile.Open(m_sMessageFileName, File::OTAppend))
         return false;
   
      DWORD dwNoOfBytesWritten = 0;
      oFile.Write(pBuf, WriteLen, dwNoOfBytesWritten);

      return true;
   }

   bool
   IMAPCommandAppend::_TruncateBuffer(const shared_ptr<IMAPConnection> pConn)
   {
      if (_appendBuffer.GetSize() >= 20000)
      {
         _WriteData(pConn, _appendBuffer.GetBuffer(), _appendBuffer.GetSize());
         m_lBytesLeftToReceive -= _appendBuffer.GetSize();
         _appendBuffer.Empty();
      }

      return true;

   }

   void
   IMAPCommandAppend::_Finish(shared_ptr<IMAPConnection> pConnection)
   {
      if (!current_message_)
         return;

      // Add this message to the folder.
      current_message_->SetSize(FileUtilities::FileSize(m_sMessageFileName));
      current_message_->SetState(Message::Delivered);

      // Set message flags.
      bool bSeen = (m_sFlagsToSet.FindNoCase(_T("\\Seen")) >= 0);
      bool bDeleted = (m_sFlagsToSet.FindNoCase(_T("\\Deleted")) >= 0);
      bool bDraft = (m_sFlagsToSet.FindNoCase(_T("\\Draft")) >= 0);
      bool bAnswered = (m_sFlagsToSet.FindNoCase(_T("\\Answered")) >= 0);
      bool bFlagged = (m_sFlagsToSet.FindNoCase(_T("\\Flagged")) >= 0);
      
      if (bSeen)
      {
         // ACL: If user tries to set the Seen flag, check that he has permission to do so.
         if (!pConnection->CheckPermission(destination_folder_, ACLPermission::PermissionWriteSeen))
         {
            // User does not have permission to set the Seen flag. 
            bSeen = false;
         }
      }

      current_message_->SetFlagDeleted(bDeleted);
      current_message_->SetFlagSeen(bSeen);
      current_message_->SetFlagDraft(bDraft);
      current_message_->SetFlagAnswered(bAnswered);
      current_message_->SetFlagFlagged(bFlagged);

      current_message_->SetFlagRecent(true);
         
      // Set the create time
      if (!m_sCreateTimeToSet.IsEmpty())
      {
         // Convert to internal format
         m_sCreateTimeToSet = Time::GetInternalDateFromIMAPInternalDate(m_sCreateTimeToSet);
         current_message_->SetCreateTime(m_sCreateTimeToSet);
      }

      PersistentMessage::SaveObject(current_message_);
      destination_folder_->SetFolderNeedsRefresh();

      String sResponse;
      if (pConnection->GetCurrentFolder() &&
          pConnection->GetCurrentFolder()->GetID() == destination_folder_->GetID())
      {
         shared_ptr<Messages> messages = destination_folder_->GetMessages();
         sResponse += IMAPNotificationClient::GenerateExistsString(messages->GetCount());
         sResponse += IMAPNotificationClient::GenerateRecentString(messages->GetNoOfRecent());
      }

      // Send the OK response to the client.
      sResponse += m_sCurrentTag + " OK APPEND completed\r\n";
      pConnection->SendAsciiData(sResponse);

      // Notify the mailbox notifier that the mailbox contents have changed. 
      shared_ptr<ChangeNotification> pNotification = 
         shared_ptr<ChangeNotification>(new ChangeNotification(destination_folder_->GetAccountID(), destination_folder_->GetID(), ChangeNotification::NotificationMessageAdded));
      Application::Instance()->GetNotificationServer()->SendNotification(pConnection->GetNotificationClient(), pNotification);

      destination_folder_.reset();
      current_message_.reset();
   }

   int 
   IMAPCommandAppend::_GetMaxMessageSize(shared_ptr<const Domain> pDomain)
   {
      int iMaxMessageSizeKB = Configuration::Instance()->GetSMTPConfiguration()->GetMaxMessageSize();

      if (pDomain)
      {
         int iDomainMaxSizeKB = pDomain->GetMaxMessageSize(); 
         if (iDomainMaxSizeKB > 0)
         {
            if (iMaxMessageSizeKB == 0 || iMaxMessageSizeKB > iDomainMaxSizeKB)
               iMaxMessageSizeKB = iDomainMaxSizeKB;
         }
      }

      return iMaxMessageSizeKB;
   }

}
