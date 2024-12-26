using FlightManagementSystem.Infrastructure;
using FlightManagementSystem.Models;
using FlightManagementSystem.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Moq;
using RabbitMQ.Client;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace FlightManagementSystemTests
{

    [TestClass]
    public class RabbitMqServiceItTests
    {
        //Pre-Requisites:
        //RabbitMQ server must be running locally or accessible at localhost:5672 with guest credentials.

        [TestMethod]
        public async Task PublishAndConsumeMessage_ShouldMatchPublishedMessage()
        {
            // Arrange: Create RabbitMQ factory and service
            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RabbitMqService>();
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672,
                VirtualHost = "/"
            };
            var rabbitMqService = new RabbitMqService(logger, factory);

            var flightMessage = new FlightNotification
            {
                FlightId = "111",
                Airline = "AirlineName",
                Origin = "OriginCity",
                Destination = "DestinationCity",
                Price = 500,
                Currency = "USD",
                DepartureDate = DateTime.Now
            };

            // Act: Publish the message
            await rabbitMqService.PublishMessageAsync(flightMessage);

            // Act: Consume the message
            FlightNotification? consumedMessage = null;
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10)); // Timeout to avoid infinite waiting

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var message = await rabbitMqService.ConsumeMessageAsync(cancellationTokenSource.Token);
               
                if (message != null)
                {
                    consumedMessage = message;
                    break;
                }

                await Task.Delay(100); // Avoid busy waiting
            }

            // Assert: Verify the consumed message matches the published message
            Assert.IsNotNull(consumedMessage, "No message was consumed from the queue.");
            Assert.AreEqual(flightMessage.FlightId, consumedMessage.FlightId);
            Assert.AreEqual(flightMessage.Airline, consumedMessage.Airline);
            Assert.AreEqual(flightMessage.Origin, consumedMessage.Origin);
            Assert.AreEqual(flightMessage.Destination, consumedMessage.Destination);
            Assert.AreEqual(flightMessage.Price, consumedMessage.Price);
            Assert.AreEqual(flightMessage.Currency, consumedMessage.Currency);
            Assert.AreEqual(flightMessage.DepartureDate.ToString("o"), consumedMessage.DepartureDate.ToString("o"));
        }
    }

    [TestClass]
    public class UserRepositoryItTests
    {
        private IMongoClient? _mongoClient;
        private UserRepository? _repository;

        [TestInitialize]
        public void Setup()
        {
            // Prerequisite: Make sure MongoDB is running locally or in Docker
            _mongoClient = new MongoClient("mongodb://localhost:27017");
            _repository = new UserRepository(_mongoClient);
        }

        [TestMethod]
        public async Task AddUserAsync_ReadAndDelete_ShouldWorkCorrectly()
        {
            // Arrange
            var user = new User { Name = "Test User", Email = "test@example.com" };

            // Act: Add user to the database
            var addedUser = await _repository!.AddUserAsync(user);

            // Assert: Verify user was added correctly
            Assert.AreEqual(user.Name, addedUser.Name);
            Assert.AreEqual(user.Email, addedUser.Email);

            // Act: Read the user by ID
            var retrievedUser = await _repository.GetUserByIdAsync(addedUser.Id!);

            // Assert: Verify retrieved user matches the added user
            Assert.IsNotNull(retrievedUser);
            Assert.AreEqual(addedUser.Id, retrievedUser.Id);
            Assert.AreEqual(addedUser.Name, retrievedUser.Name);
            Assert.AreEqual(addedUser.Email, retrievedUser.Email);

            // Act: Delete the user
            User? deleteResult = await _repository.DeleteUserAsync(addedUser.Id!);

            // Assert: Verify deletion was successful
            Assert.IsTrue(deleteResult != null);

            // Verify the user no longer exists in the database
            var deletedUser = await _repository.GetUserByIdAsync(addedUser.Id!);
            Assert.IsNull(deletedUser);
        }
    }

    [TestClass]
    public class PushNotificationServiceItTests
    {
        [TestMethod]
        public void SendPushAlert_ShouldLogMessage()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PushNotificationService>>();
           
            var service = new PushNotificationService(loggerMock.Object);

            var user = new User { Name = "John Doe", Email = "johnDoe@gmail.com" };

            var flightMessage = new FlightNotification
            {
                FlightId = "111",
                Airline = "AirlineName",
                Origin = "OriginCity",
                Destination = "DestinationCity",
                Price = 500,
                Currency = "USD",
                DepartureDate = DateTime.Now
            };

            var notificationMessage = $"Hi {user.Name}, the flight '{flightMessage.Airline}' from {flightMessage.Origin} to {flightMessage.Destination} is now available for {flightMessage.Price} {flightMessage.Currency}.";

            // Act
            service.SendPushAlert(notificationMessage);

            // Assert
            loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, t) => state.ToString()!.Contains($"Push alert sent: {notificationMessage}")),
                        It.IsAny<Exception?>(),
                        It.Is<Func<It.IsAnyType, Exception?, string>>((state, exception) => true)), // Accept any valid formatter
                    Times.Once
                );

        }
    }

    [TestClass]
    public class UserServiceItTests
    {
        private IUserService? _userService;
        private IUserRepository? _userRepository;

        [TestInitialize]
        public void TestInitialize()
        {
            // Replace the connection string and database name with your environment details
            _userRepository = new UserRepository(new MongoClient("mongodb://localhost:27017"));
            _userService = new UserService(_userRepository);
        }

        [TestMethod]
        public async Task UserAlertPreferencesIntegrationTest()
        {
            // Step 1: Create a user
            var user = new User
            {
                Name = "Test User 123",
                Email = "test123@example.com",
                MobileDeviceToken = "TestDeviceToken123"
            };
            var createdUser = await _userService!.AddUserAsync(user);
            Assert.IsNotNull(createdUser);
            Assert.AreEqual(user.Name, createdUser.Name);

            // Step 2: Define and add two UserAlertPreferences for the user
            var alertPreference1 = new AlertPreference
            {
                PreferenceId = Guid.NewGuid().ToString(),
                Destination = "New York",
                MaxPrice = 500,
                Currency = "USD"
            };
            var alertPreference2 = new AlertPreference
            {
                PreferenceId = Guid.NewGuid().ToString(),
                Destination = "Paris",
                MaxPrice = 600,
                Currency = "EUR"
            };

            var userWithPreference1 = await _userService.AddAlertPreferenceAsync(createdUser.Id!, alertPreference1);
            Assert.IsNotNull(userWithPreference1);
            Assert.AreEqual(1, userWithPreference1.AlertPreferences.Count);

            var userWithPreference2 = await _userService.AddAlertPreferenceAsync(createdUser.Id!, alertPreference2);
            Assert.IsNotNull(userWithPreference2);
            Assert.AreEqual(2, userWithPreference2.AlertPreferences.Count);

            // Step 3: Read the user's preferences
            var userWithPreferences = await _userService.GetUserByIdAsync(createdUser.Id!);
            Assert.IsNotNull(userWithPreferences!.AlertPreferences);
            Assert.AreEqual(2, userWithPreferences.AlertPreferences.Count);

            // Step 4: Update one of the UserAlertPreferences
            var updatedPreference = new AlertPreference
            {
                PreferenceId = alertPreference1.PreferenceId,
                Destination = alertPreference1.Destination,
                MaxPrice = 450, // Adjust the MaxPrice
                Currency = alertPreference1.Currency
            };
            var updatedUser = await _userService.UpdateAlertPreferenceAsync(createdUser.Id!, alertPreference1.PreferenceId!, updatedPreference);
            Assert.IsNotNull(updatedUser);

            var updatedAlertPreference = updatedUser.AlertPreferences.First(p => p.PreferenceId == alertPreference1.PreferenceId);
            Assert.AreEqual(450, updatedAlertPreference.MaxPrice);

            // Step 5: Remove both UserAlertPreferences
            var userAfterFirstRemoval = await _userService.DeleteAlertPreferenceAsync(createdUser.Id!, alertPreference1.PreferenceId!);
            Assert.IsNotNull(userAfterFirstRemoval);
            Assert.AreEqual(1, userAfterFirstRemoval.AlertPreferences.Count);

            var userAfterSecondRemoval = await _userService.DeleteAlertPreferenceAsync(createdUser.Id!, alertPreference2.PreferenceId!);
            Assert.IsNotNull(userAfterSecondRemoval);
            Assert.AreEqual(0, userAfterSecondRemoval.AlertPreferences.Count);

            await _userService.DeleteUserAsync(createdUser.Id!);
        }
    }

    /*
    [TestClass]
    public class FlightPriceServiceItTests
    {
        [TestMethod]
        public async Task IntegrationTest_FetchFlightPrices_ShouldlnotPublishMessages()
        {
            // Arrange
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
        
            var rabbitMqService = new RabbitMqService(new LoggerFactory().CreateLogger<RabbitMqService>(), factory);

            // var httpClientFactory = new HttpClientFactoryMock(); // Replace with mock or test HttpClientFactory
            var logger = new LoggerFactory().CreateLogger<FlightPriceService>();

            Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "FlightAPIs:0", "https://mock-flight-api.com/prices" }
                }).Build();

            var service = new FlightPriceService(logger, configuration, httpClientFactoryMock.Object, rabbitMqService);

            // Act
            await service.FetchFlightPricesAsync(CancellationToken.None);

            // Assert
            // Consume from RabbitMQ and verify messages
            var cancellationTokenSource = new CancellationTokenSource();
           
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(4)); //since the API is fake the consume will fail too
          
            var consumedMessage = await rabbitMqService.ConsumeMessageAsync(cancellationTokenSource.Token);
          
            Assert.IsNull(consumedMessage);
        }
    }
    */

    [TestClass]
    public class NotificationServiceItTests
    {
        private IMongoClient? _mongoClient;
        private UserRepository? _repository;

        [TestInitialize]
        public void Setup()
        {
            // Prerequisite: Make sure MongoDB is running locally or in Docker
            _mongoClient = new MongoClient("mongodb://localhost:27017");
            _repository = new UserRepository(_mongoClient);
        }

        [TestMethod]
        public async Task IntegrationTest_NotificationService_ShouldLogMatchedNotification()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PushNotificationService>>();
            var userService = new UserService(_repository!);

            var user1 = await userService.AddUserAsync(new User { Name = "User1", Email = "user1@gmail.com" });
            var user2 = await userService.AddUserAsync(new User { Name = "User2", Email = "user2@gmail.com" });

            user1 = await userService.AddAlertPreferenceAsync(user1.Id!, new AlertPreference { MaxPrice = 1000, Destination = "Paris", Currency = "USD" }); // Alert
            user1 = await userService.AddAlertPreferenceAsync(user1!.Id!, new AlertPreference { MaxPrice = 700, Destination = "Rome", Currency = "USD" }); // Alert
            user1 = await userService.AddAlertPreferenceAsync(user1!.Id!, new AlertPreference { MaxPrice = 1300, Destination = "Zurich", Currency = "USD" }); // No Alert

            user2 = await userService.AddAlertPreferenceAsync(user2.Id!, new AlertPreference { MaxPrice = 500, Destination = "London", Currency = "USD" }); // Alert
            user2 = await userService.AddAlertPreferenceAsync(user2!.Id!, new AlertPreference { MaxPrice = 1200, Destination = "Zurich", Currency = "USD" }); // Alert

            var pushNotificationServiceMock = new Mock<IPushNotificationService>();
            pushNotificationServiceMock
                .Setup(x => x.SendPushAlert(It.IsAny<string>()))
                .Callback<string>(message => loggerMock.Object.LogInformation($"Push alert sent: {message}"));

            var rabbitMqService = new RabbitMqService(new Mock<ILogger<RabbitMqService>>().Object, new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            });

            try
            {
                var notificationService = new NotificationService(
                    userService,
                    rabbitMqService,
                    pushNotificationServiceMock.Object
                );

                // Publish test messages to RabbitMQ
                var flightMessages = new List<FlightNotification>
                {
                    new FlightNotification { FlightId = "100", Airline = "Airline3", Origin = "Tel-Aviv", Destination = "Zurich", Price = 1500, Currency = "USD", DepartureDate = DateTime.Now.AddDays(2) },
                    new FlightNotification { FlightId = "101", Airline = "Airline1", Origin = "Tel-Aviv", Destination = "London", Price = 400, Currency = "USD", DepartureDate = DateTime.Now.AddDays(3) }, 
                    new FlightNotification { FlightId = "102", Airline = "Airline2", Origin = "Tel-Aviv", Destination = "Paris", Price = 600, Currency = "USD", DepartureDate = DateTime.Now.AddDays(4) }, 
                    new FlightNotification { FlightId = "104", Airline = "Airline2", Origin = "Tel-Aviv", Destination = "Rome", Price = 600, Currency = "USD", DepartureDate = DateTime.Now.AddDays(5) }, 
                    new FlightNotification { FlightId = "105", Airline = "Airline3", Origin = "Tel-Aviv", Destination = "Zurich", Price = 1200, Currency = "USD", DepartureDate = DateTime.Now.AddDays(1) } 
                };

                foreach (var flightMessage in flightMessages)
                {
                    await rabbitMqService.PublishMessageAsync(flightMessage);
                }

                await Task.Delay(1000); // 1-second delay for RabbitMQ to process

                uint queueSize = await rabbitMqService.GetQueueSize("FlightPricesQueue");

                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(7));

                // Act
                _ = notificationService.StartAsync(cancellationTokenSource.Token);

                // Wait for processing
                await Task.Delay(8000);

                // Assert
                loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, t) => state.ToString()!.Contains("Push alert sent: Hi User1")),
                        It.IsAny<Exception?>(),
                        It.Is<Func<It.IsAnyType, Exception?, string>>((state, exception) => true)),
                    Times.Exactly(3), // User1 should receive 4 alerts
                    "Expected 3 notifications for User1 were not logged."
                );

                loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, t) => state.ToString()!.Contains("Push alert sent: Hi User2")),
                        It.IsAny<Exception?>(),
                        It.Is<Func<It.IsAnyType, Exception?, string>>((state, exception) => true)),
                    Times.Exactly(2), // User2 should receive 2 alerts
                    "Expected 2 notifications for User2 were not logged."
                );

                // Clean up
                cancellationTokenSource.Cancel();

                if (user1 != null)
                {
                    await userService.DeleteUserAsync(user1.Id!);
                }
                if (user2 != null)
                {
                    await userService.DeleteUserAsync(user2.Id!);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
            finally
            {
                try
                {
                    if (user1 != null)
                    {
                        Console.WriteLine($"Deleting User1: {user1.Id}");
                        await userService.DeleteUserAsync(user1.Id!);
                    }
                    if (user2 != null)
                    {
                        Console.WriteLine($"Deleting User2: {user2.Id}");
                        await userService.DeleteUserAsync(user2.Id!);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }
            }
        }
    }
}
