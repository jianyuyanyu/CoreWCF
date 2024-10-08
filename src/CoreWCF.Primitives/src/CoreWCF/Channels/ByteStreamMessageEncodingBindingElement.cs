﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class ByteStreamMessageEncodingBindingElement : MessageEncodingBindingElement
    {
        private readonly XmlDictionaryReaderQuotas _readerQuotas;
        private bool _moveBodyReaderToContent;

        public ByteStreamMessageEncodingBindingElement() : this((XmlDictionaryReaderQuotas)null)
        {
        }

        public ByteStreamMessageEncodingBindingElement(XmlDictionaryReaderQuotas quota)
        {
            _readerQuotas = new XmlDictionaryReaderQuotas();
            if (quota != null)
            {
                quota.CopyTo(_readerQuotas);
            }
        }

        private ByteStreamMessageEncodingBindingElement(ByteStreamMessageEncodingBindingElement byteStreamEncoderBindingElement)
            : this(byteStreamEncoderBindingElement._readerQuotas)
        {
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return MessageVersion.None;
            }
            set
            {
                if (value != MessageVersion.None)
                {
                    throw Fx.Exception.Argument(nameof(MessageVersion), SR.Format(SR.ByteStreamMessageEncoderMessageVersionNotSupported, value));
                }
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return _readerQuotas;
            }
            set
            {
                if (value == null)
                    throw Fx.Exception.ArgumentNull(nameof(ReaderQuotas));
                value.CopyTo(ReaderQuotas);
            }
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T).FullName == "CoreWCF.Channels.WebMessageEncoderFactory+WebMessageEncoder")
            {
                _moveBodyReaderToContent = true;
                return null;
            }

            return base.GetProperty<T>(context);
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory() => new ByteStreamMessageEncoderFactory(_readerQuotas, _moveBodyReaderToContent);

        public override BindingElement Clone() => new ByteStreamMessageEncodingBindingElement(this);
    }
}
