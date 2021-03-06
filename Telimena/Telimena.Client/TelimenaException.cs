﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TelimenaClient
{
    /// <summary>
    ///     Class TelimenaException.
    /// </summary>
    /// <seealso cref="System.Exception" />
    public class TelimenaException : Exception
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TelimenaException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public TelimenaException(string message) : base(message)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TelimenaException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="telimenaProperties"></param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="requestObjects">The request objects.</param>
        public TelimenaException(string message, ITelimenaProperties telimenaProperties, Exception innerException, params KeyValuePair<Type, object>[] requestObjects) : base(message, innerException)
        {
            this.TelimenaProperties = telimenaProperties;
            this.RequestObjects = requestObjects;
            if (innerException is AggregateException exception)
            {
                this.InnerExceptions = exception.InnerExceptions;
            }
            else
            {
                this.InnerExceptions = new ReadOnlyCollection<Exception>(new[] {innerException});
            }
        }

        /// <summary>
        /// Telimena Client data
        /// </summary>
        public ITelimenaProperties TelimenaProperties { get; }

        /// <summary>
        ///     Gets the request objects.
        /// </summary>
        /// <value>The request objects.</value>
        public KeyValuePair<Type, object>[] RequestObjects { get; }

        /// <summary>
        ///     Gets the inner exceptions.
        /// </summary>
        /// <value>The inner exceptions.</value>
        public ReadOnlyCollection<Exception> InnerExceptions { get; }
    }
}