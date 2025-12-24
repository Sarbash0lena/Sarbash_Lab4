using System;
using System.Collections.Generic;
using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;
using Xunit;

namespace Library_Project_Tests
{
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        /// <summary>
        /// Перевірка додавання нової книги, якщо вона ще не існує в бібліотеці
        /// </summary>
        [Fact]
        public void AddBook_ShouldAddNewBook_WhenNotExists()
        {
            _repoMock.Setup(r => r.FindBook("1984")).Returns((Book)null);

            _service.AddBook("1984", 3);

            _repoMock.Verify(
                r => r.SaveBook(It.Is<Book>(b => b.Title == "1984" && b.Copies == 3)),
                Times.Once
            );
        }

        /// <summary>
        /// Перевірка збільшення кількості примірників, якщо книга вже існує
        /// </summary>
        [Fact]
        public void AddBook_ShouldIncreaseCopies_WhenBookExists()
        {
            var existing = new Book { Title = "1984", Copies = 2 };
            _repoMock.Setup(r => r.FindBook("1984")).Returns(existing);

            _service.AddBook("1984", 3);

            Assert.Equal(5, existing.Copies);
            _repoMock.Verify(r => r.SaveBook(existing), Times.Once);
        }

        /// <summary>
        /// Перевірка викидання винятку при невалідних вхідних даних
        /// </summary>
        [Theory]
        [InlineData("", 2)]
        [InlineData("Book", 0)]
        public void AddBook_ShouldThrow_WhenInvalidInput(string title, int copies)
        {
            Assert.ThrowsAny<ArgumentException>(() => _service.AddBook(title, copies));
        }

        /// <summary>
        /// Перевірка успішного позичання книги валідним користувачем
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldDecreaseCopies_WhenValidMemberAndAvailable()
        {
            var book = new Book { Title = "Dune", Copies = 2 };
            _repoMock.Setup(r => r.FindBook("Dune")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            bool result = _service.BorrowBook(1, "Dune");

            Assert.True(result);
            Assert.Equal(1, book.Copies);
            _notifMock.Verify(n => n.NotifyBorrow(1, "Dune"), Times.Once);
        }

        /// <summary>
        /// Перевірка повернення false, якщо немає доступних примірників книги
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldReturnFalse_WhenNoCopies()
        {
            var book = new Book { Title = "Dune", Copies = 0 };
            _repoMock.Setup(r => r.FindBook("Dune")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            bool result = _service.BorrowBook(1, "Dune");

            Assert.False(result);
            _notifMock.Verify(
                n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never
            );
        }

        /// <summary>
        /// Перевірка викидання винятку при невалідному користувачі
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldThrow_WhenInvalidMember()
        {
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(false);

            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(1, "Dune"));
        }

        /// <summary>
        /// Перевірка успішного повернення книги
        /// </summary>
        [Fact]
        public void ReturnBook_ShouldIncreaseCopies()
        {
            var book = new Book { Title = "Dune", Copies = 1 };
            _repoMock.Setup(r => r.FindBook("Dune")).Returns(book);

            bool result = _service.ReturnBook(1, "Dune");

            Assert.True(result);
            Assert.Equal(2, book.Copies);
            _notifMock.Verify(n => n.NotifyReturn(1, "Dune"), Times.Once);
        }

        /// <summary>
        /// Перевірка повернення false, якщо книгу не знайдено
        /// </summary>
        [Fact]
        public void ReturnBook_ShouldReturnFalse_WhenBookNotFound()
        {
            _repoMock.Setup(r => r.FindBook("Unknown")).Returns((Book)null);

            bool result = _service.ReturnBook(1, "Unknown");

            Assert.False(result);
        }

        /// <summary>
        /// Перевірка отримання лише доступних книг (кількість > 0)
        /// </summary>
        [Fact]
        public void GetAvailableBooks_ShouldReturnOnlyBooksWithCopies()
        {
            var all = new List<Book>
            {
                new Book { Title = "A", Copies = 0 },
                new Book { Title = "B", Copies = 1 },
                new Book { Title = "C", Copies = 3 },
            };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(all);

            var available = _service.GetAvailableBooks();

            Assert.NotEmpty(available);
            Assert.Contains(available, b => b.Title == "B");
            Assert.Equal(2, available.Count);
        }

        /// <summary>
        /// Перевірка повернення порожнього списку, якщо доступних книг немає
        /// </summary>
        [Fact]
        public void GetAvailableBooks_ShouldReturnEmpty_WhenNoBooksAvailable()
        {
            var all = new List<Book>
            {
                new Book { Title = "A", Copies = 0 },
            };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(all);

            var result = _service.GetAvailableBooks();

            Assert.Empty(result);
        }

        /// <summary>
        /// Перевірка того, що метод пошуку книги викликається хоча б один раз
        /// </summary>
        [Fact]
        public void Verify_MethodsCalled_AtLeastOnce()
        {
            var book = new Book { Title = "Dune", Copies = 1 };
            _repoMock.Setup(r => r.FindBook("Dune")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            _service.BorrowBook(1, "Dune");

            _repoMock.Verify(r => r.FindBook("Dune"), Times.AtLeastOnce);
        }

        /// <summary>
        /// Приклад використання It.Is з предикатом для аргументу
        /// </summary>
        [Fact]
        public void It_Is_PredicateExample()
        {
            var book = new Book { Title = "Dune", Copies = 2 };
            _repoMock.Setup(r => r.FindBook(It.Is<string>(s => s.StartsWith("D")))).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            var result = _service.BorrowBook(1, "Dune");

            Assert.True(result);
        }

        /// <summary>
        /// Приклад використання It.IsAny для прийняття будь-якого значення аргументу
        /// </summary>
        [Fact]
        public void It_IsAny_ShouldMatchAnyTitle()
        {
            var book = new Book { Title = "Anything", Copies = 2 };
            _repoMock.Setup(r => r.FindBook(It.IsAny<string>())).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            bool result = _service.BorrowBook(1, "RandomTitle");

            Assert.True(result);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }
    }
}
