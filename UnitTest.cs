//using FlightManagementSystem.Infrastructure;
//using FlightManagementSystem.Models;
//using FlightManagementSystem.Services;
//using Microsoft.Extensions.Logging;
//using MongoDB.Driver;
//using Moq;
//using RabbitMQ.Client;
//using System.Text;

//namespace FlightManagementSystemTests
//{

//    [TestClass]
//    public class RabbitMqServiceTests
//    {
//        [TestMethod]
//        public async Task PublishMessageAsync_ShouldPublishMessage()
//        {
//            // Arrange
//            var loggerMock = new Mock<ILogger<RabbitMqService>>();
          
//            var factory = new ConnectionFactory
//            {
//                HostName = "localhost",
//                UserName = "guest",
//                Password = "guest",
//                Port = 5672,
//                VirtualHost = "/"
//            };
//            var service = new RabbitMqService(loggerMock.Object, factory);

//            var flightMessage = new FlightNotification
//            {
//                FlightId = "111",
//                Airline = "AirlineName",
//                Origin = "OriginCity",
//                Destination = "DestinationCity",
//                Price = 500,
//                Currency = "USD",
//                DepartureDate = DateTime.Now
//            };

//            // Act (fake publish, adapt with real RabbitMQ mocking)
//            await service.PublishMessageAsync(flightMessage);

//            // Assert
//            // Verify logs or RabbitMQ interactions as appropriate
//        }
//    }

//    [TestClass]
//    public class UserRepositoryTests
//    {
//        [TestMethod]
//        public async Task AddUserAsync_ShouldAddUser()
//        {
//            // Arrange
//            var mongoCollectionMock = new Mock<IMongoCollection<User>>();
//            var dbMock = new Mock<IMongoDatabase>();
//            dbMock.Setup(db => db.GetCollection<User>(It.IsAny<string>(), null)).Returns(mongoCollectionMock.Object);

//            var repo = new UserRepository((IMongoClient)dbMock!.Object);
//            var user = new User { Name = "Test User", Email = "test@example.com" };

//            // Act
//            var result = await repo.AddUserAsync(user);

//            // Assert
//            Assert.AreEqual(user.Name, result.Name);
//            mongoCollectionMock.Verify(c => c.InsertOneAsync(user, null, default), Times.Once);
//        }
//    }

//    [TestClass]
//    public class PushNotificationServiceTests
//    {
//        [TestMethod]
//        public void SendPushAlert_ShouldLogMessage()
//        {
//            // Arrange
//            var loggerMock = new Mock<ILogger<PushNotificationService>>();
//            var service = new PushNotificationService(loggerMock.Object);

//            var user = new User { Name = "John Doe", Email = "johnDoe@gmail.com" };

//            var flightMessage = new FlightNotification
//            {
//                FlightId = "111",
//                Airline = "AirlineName",
//                Origin = "OriginCity",
//                Destination = "DestinationCity",
//                Price = 500,
//                Currency = "USD",
//                DepartureDate = DateTime.Now
//            };

//            var notificationMessage = $"Hi {user.Name}, the flight '{flightMessage.Airline}' from {flightMessage.Origin} to {flightMessage.Destination} is now available for {flightMessage.Price} {flightMessage.Currency}.";

//            // Act
//            service.SendPushAlert(notificationMessage);

//            // Assert
//            // Verify console output using redirection or mock approach
//        }
//    }

//    [TestClass]
//    public class FlightPriceServiceTests
//    {
//        [TestMethod]
//        public async Task FetchFlightPricesAsync_ShouldLogFetching()
//        {
//            // Arrange
//            var loggerMock = new Mock<ILogger<FlightPriceService>>();
//            //var configMock = null;// new Mock<IConfiguration>();
//            var rabbitMqServiceMock = new Mock<IRabbitMqService>();

//            var service = new FlightPriceService(loggerMock.Object, /*configMock.Object*/null, null, rabbitMqServiceMock.Object);
//            var cancellationToken = CancellationToken.None;

//            // Act
//            await service.FetchFlightPricesAsync(cancellationToken);

//            // Assert
//            loggerMock.Verify(l => l.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
//        }
//    }

//    [TestClass]
//    public class NotificationServiceTests
//    {
//        [TestMethod]
//        public async Task ProcessNotification_ShouldSendAlerts()
//        {
//            // Arrange
//            var userServiceMock = new Mock<IUserService>();
//            var rabbitMqServiceMock = new Mock<IRabbitMqService>();
//            var pushNotificationServiceMock = new Mock<IPushNotificationService>();

//            var service = new NotificationService(userServiceMock.Object, rabbitMqServiceMock.Object, pushNotificationServiceMock.Object);
        
//            var cancellationToken = CancellationToken.None;

//            // Act
//            await service.StartAsync(cancellationToken);

//            // Assert
//            pushNotificationServiceMock.Verify(p => p.SendPushAlert(It.IsAny<string>()), Times.AtLeastOnce);
//        }
//    }

//    [TestClass]
//    public class UserServiceTests
//    {
//        [TestMethod]
//        public async Task AddUserAsync_ShouldAddUserViaRepository()
//        {
//            // Arrange
//            var repoMock = new Mock<IUserRepository>();
//            var service = new UserService(repoMock.Object);
//            var user = new User { Name = "Test User", Email = "test@example.com" };

//            // Act
//            var result = await service.AddUserAsync(user);

//            // Assert
//            Assert.AreEqual(user.Name, result.Name);
//            repoMock.Verify(r => r.AddUserAsync(user), Times.Once);
//        }
//    }
//}