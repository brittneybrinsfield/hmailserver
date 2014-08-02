// Copyright (c) 2010 Martin Knafve / hMailServer.com.  
// http://www.hmailserver.com

#include "stdafx.h"

#include "Attachment.h"

#include "../Mime/Mime.h"

#ifdef _DEBUG
#define DEBUG_NEW new(_NORMAL_BLOCK, __FILE__, __LINE__)
#define new DEBUG_NEW
#endif

namespace HM
{
   Attachment::Attachment(shared_ptr<MimeBody> pMessage, shared_ptr<MimeBody> pAttachment) :
      message_(pMessage),
      attachment_(pAttachment)
   {

   }

   Attachment::~Attachment()
   {

   }

   void
   Attachment::SaveAs(const String &sSaveTo) const
   {
      attachment_->WriteToFile(sSaveTo);
   }

   String 
   Attachment::GetFileName()
   {
      return attachment_->GetUnicodeFilename();
   }

   int
   Attachment::GetSize()
   {
      return attachment_->GetContentLength();
   }

   void
   Attachment::Delete()
   {
      // Remove this attachment from the parent message.
      message_->RemoveAttachment(attachment_);
   }
}
