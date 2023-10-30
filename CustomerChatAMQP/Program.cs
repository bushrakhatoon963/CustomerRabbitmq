using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading;
using Serilog;

namespace CustomerChatAMQP
{
    class Program
    {
        static bool pollingEnabled = false;

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("server.log", rollingInterval: RollingInterval.Day).CreateLogger();
            Log.Information("Customer started.");
            Console.Title = "Customers";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = "localhost", // RabbitMQ server address
                    UserName = "guest",
                    Password = "guest"
                };
                var connection = factory.CreateConnection();
                var customerChannel = connection.CreateModel();

                var customerId = string.Format("Customer{0}", DateTime.Now.ToString("yyyyMMddHHmmss"));
                Log.Information("Customer {0} connected.", customerId);
                // Log errors and chat interactions as needed
                // Queue for receiving responses from the server
                customerChannel.QueueDeclare(queue: "customer_response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                Console.WriteLine("Enter your message (or 'exit' to quit):");
                while (true)
                {
                    var message = Console.ReadLine();

                    if (message.ToLower() == "exit")
                    {
                        break;
                    }

                    // Send message to the server for assistance
                    var customerMessage = string.Format("{0}:{1}", customerId, message);
                    var customerMessageBody = Encoding.UTF8.GetBytes(customerMessage);
                    //Console.WriteLine(customerMessage);
                    customerChannel.BasicPublish(exchange: "", routingKey: "customer_queue", basicProperties: null, body: customerMessageBody);
                    // Listen for response from the server
                    var customerConsumer = new EventingBasicConsumer(customerChannel);
                    customerConsumer.Received += (model, ea) =>
                    {
                        var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                        Console.WriteLine(string.Format("{0}", response));
                        // Inside the while loop where customer sends messages and listens for responses
                        if (response.Trim().ToLower() == "ok" && !pollingEnabled)
                        {
                            pollingEnabled = true;
                            Timer timer = new Timer(PollServer, customerChannel, TimeSpan.Zero, TimeSpan.FromSeconds(1));
                        }

                    };
                    customerChannel.BasicConsume(queue: "customer_response_queue", autoAck: true, consumer: customerConsumer);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred: {ErrorMessage}", ex.Message);
            }            
        }

        static void PollServer(object state)
        {
            var customerChannel = (IModel)state;
            // Implement logic to poll the server for new messages here
            // For example, you can request new messages from the server and handle the responses
            // Sample logic:
            // Request new messages from the server
            string request = "new_messages_request"; // Define a protocol for requesting new messages
            var requestBytes = Encoding.UTF8.GetBytes(request);
            customerChannel.BasicPublish(exchange: "", routingKey: "server_queue", basicProperties: null, body: requestBytes);
            // Listen for response from the server
            var consumer = new EventingBasicConsumer(customerChannel);
            consumer.Received += (model, ea) =>
            {
                var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                // Handle the received message
                Console.WriteLine(string.Format("Received new message: {response}"));
            };
            customerChannel.BasicConsume(queue: "customer_response_queue", autoAck: true, consumer: consumer);
        }        
    }
}
