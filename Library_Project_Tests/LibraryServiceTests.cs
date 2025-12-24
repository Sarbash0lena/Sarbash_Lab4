using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

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
        /// Перевіряє, що нова книга додається до репозиторію,
        /// якщо книга з такою назвою ще не існує.
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
        /// Перевіряє, що кількість примірників книги збільшується,
        /// якщо книга вже існує у репозиторії.
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
        /// Перевіряє, що метод AddBook генерує виняток
        /// у разі передачі некоректних вхідних даних.
        /// </summary>
        [Theory]
        [InlineData("", 2)]
        [InlineData("Book", 0)]
        public void AddBook_ShouldThrow_WhenInvalidInput(string title, int copies)
        {
            Assert.ThrowsAny<ArgumentException>(() => _service.AddBook(title, copies));
        }

        /// <summary>
        /// Перевіряє, що книга успішно видається користувачу,
        /// якщо користувач валідний і примірники доступні.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldDecreaseCopies_WhenValidMemberAndAvailable()
        {
            var book = new Book { Title = "Dune", Copies = 2 };
            _repoMock.Setup(r => r.FindBook("Dune")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            var result = _service.BorrowBook(1, "Dune");

            Assert.True(result);
            Assert.Equal(1, book.Copies);
            _notifMock.Verify(n => n.NotifyBorrow(1, "Dune"), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що книга не видається,
        /// якщо кількість доступних примірників дорівнює нулю.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldReturnFalse_WhenNoCopies()
        {
            var book = new Book { Title = "Dune", Copies = 0 };
            _repoMock.Setup(r => r.FindBook("Dune")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            var result = _service.BorrowBook(1, "Dune");

            Assert.False(result);
            _notifMock.Verify(
                n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never
            );
        }

        /// <summary>
        /// Перевіряє, що метод BorrowBook генерує виняток,
        /// якщо користувач не є валідним.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldThrow_WhenInvalidMember()
        {
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(false);

            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(1, "Dune"));
        }

        /// <summary>
        /// Перевіряє, що при поверненні книги
        /// кількість примірників збільшується.
        /// </summary>
        [Fact]
        public void ReturnBook_ShouldIncreaseCopies()
        {
            var book = new Book { Title = "Dune", Copies = 1 };
            _repoMock.Setup(r => r.FindBook("Dune")).Returns(book);

            var result = _service.ReturnBook(1, "Dune");

            Assert.True(result);
            Assert.Equal(2, book.Copies);
            _notifMock.Verify(n => n.NotifyReturn(1, "Dune"), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що повернення книги неможливе,
        /// якщо книга відсутня у системі.
        /// </summary>
        [Fact]
        public void ReturnBook_ShouldReturnFalse_WhenBookNotFound()
        {
            _repoMock.Setup(r => r.FindBook("Unknown")).Returns((Book)null);

            var result = _service.ReturnBook(1, "Unknown");

            Assert.False(result);
        }

        /// <summary>
        /// Перевіряє, що метод повертає лише книги,
        /// у яких кількість примірників більша за нуль.
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

            Assert.Equal(2, available.Count);
            Assert.Contains(available, b => b.Title == "B");
        }

        /// <summary>
        /// Перевіряє, що метод повертає порожній список,
        /// якщо жодна книга не доступна.
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
        /// Перевіряє, що метод FindBook викликається
        /// при спробі видати книгу.
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
        /// Перевіряє використання предиката It.Is
        /// для пошуку книги за умовою.
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
        /// Перевіряє використання It.IsAny
        /// для прийняття будь-якого значення аргументу.
        /// </summary>
        [Fact]
        public void It_IsAny_ShouldMatchAnyTitle()
        {
            var book = new Book { Title = "Anything", Copies = 2 };
            _repoMock.Setup(r => r.FindBook(It.IsAny<string>())).Returns(book);

            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            var result = _service.BorrowBook(1, "RandomTitle");

            Assert.True(result);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }
    }
}
