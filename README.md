# **Wind Data Receiver Service**

This .NET-based microservice receives wind data from a serial COM port, processes it, and sends it to another service via RabbitMQ. Designed to work with a wind testing device, the service ensures reliable data parsing and transmission even in case of corrupted or fragmented input.

## **Features**
* Serial Port Communication: Receives raw messages from a wind tester device via a COM port.
* Message Parsing: Extracts wind speed and direction from incoming data.
* Fragment Handling: Detects and reconstructs broken or concatenated messages.
* Error Handling: Robust handling of invalid or malformed messages.
* Message Forwarding: Sends processed and validated data to another microservice via RabbitMQ.

## **Technologies Used**
* ASP.NET Core
* RabbitMQ
* System.IO.Ports for serial communication
* Async processing for non-blocking I/O

## Architecture Overview
* This service is part of a two-service system:
* Receiver Service (this project): Reads and parses data from a serial COM port and sends messages to RabbitMQ.
* Storage Service: Consumes the messages, stores them, and provides access via an API.
