# Діаграми реалізованого коду

Стан: 08.11.2026. Схеми Mermaid відтворюють реалізацію; для вставлення в записку їх потрібно оформити за вимогами МВ.

## Модель предметної області

```mermaid
classDiagram
  class MenuItem {
    <<abstract>>
    +Guid Id
    +string Name
    +string Category
    +decimal Price
    +decimal PortionSize
    +bool IsAvailable
    +string PortionUnit*
    +UpdateDetails(name, category, price, portionSize) void
    +SetAvailability(isAvailable) void
    +CalculatePrice() decimal*
  }
  class Dish {
    +CalculatePrice() decimal
  }
  class Drink {
    +CalculatePrice() decimal
  }
  class ComboSet {
    +IReadOnlyList~MenuItem~ Items
    +decimal DiscountPercent
    +SetDiscount(discountPercent) void
    +CalculatePrice() decimal
  }
  class IDiscountable {
    <<interface>>
    +decimal DiscountPercent
    +SetDiscount(discountPercent) void
  }
  class Order {
    -List~OrderLine~ _lines
    +Guid Id
    +int TableNumber
    +DateTimeOffset CreatedAt
    +OrderStatus Status
    +IReadOnlyList~OrderLine~ Lines
    +decimal Total
    +AddLine(item, quantity) void
    +ChangeQuantity(lineId, quantity) void
    +RemoveLine(lineId) void
    +ChangeTable(tableNumber) void
    +ChangeStatus(next) void
  }
  class OrderLine {
    +Guid Id
    +Guid MenuItemId
    +string Name
    +string Category
    +decimal UnitPrice
    +int Quantity
    +decimal Total
  }
  class OrderStatus {
    <<enumeration>>
    New
    Preparing
    Served
    Paid
    Cancelled
  }
  class RestaurantData
  class SalesReport {
    +string GeneratedAt
    +int PaidOrders
    +decimal Total
    +List~CategorySales~ Categories
  }
  class CategorySales {
    +string Category
    +long Quantity
    +decimal Revenue
  }
  MenuItem <|-- Dish
  MenuItem <|-- Drink
  MenuItem <|-- ComboSet
  IDiscountable <|.. ComboSet
  ComboSet "0..*" o-- "2..*" MenuItem : лише Dish або Drink
  Order "1" *-- "0..*" OrderLine
  Order --> OrderStatus
  Order ..> MenuItem : AddLine
  RestaurantData o-- MenuItem
  RestaurantData o-- Order
  SalesReport *-- CategorySales
```

Сетери властивостей MenuItem та Order закриті. OrderLine має властивості лише для читання.
MenuItemId є історичним ідентифікатором, а не посиланням на живий об'єкт.
Для читабельності на схемі показано основні методи; повні сигнатури конструкторів наведено у коді.

## Сервіси, збереження та MVVM

```mermaid
classDiagram
  class IRestaurantRepository {
    <<interface>>
  }
  IRestaurantRepository <|.. JsonRestaurantRepository
  JsonRestaurantRepository ..> RestaurantMapper
  JsonRestaurantRepository ..> AtomicFile
  XmlSalesReportExporter ..> AtomicFile
  XmlSalesReportExporter ..> SalesReport
  RestaurantMapper ..> RestaurantDocument
  RestaurantMapper ..> RestaurantData
  RestaurantDocument *-- MenuItemData
  RestaurantDocument *-- OrderData
  OrderData *-- OrderLineData
  RestaurantService *-- RestaurantDocument
  RestaurantService ..> RestaurantMapper
  SalesReportService ..> SalesReport
  SalesReportService ..> OperationProgress
  JsonRestaurantRepository ..> OperationProgress
  BenchmarkService ..> SalesReportService
  BenchmarkService ..> BenchmarkResult
  DemoDataGenerator ..> RestaurantData
  ViewModelBase <|-- MainViewModel
  MainViewModel ..> RestaurantService
  MainViewModel ..> IRestaurantRepository
  MainViewModel ..> SalesReportService
  MainViewModel ..> XmlSalesReportExporter
  MainViewModel ..> BenchmarkService
  MainViewModel ..> DemoDataGenerator
  MainViewModel *-- AsyncCommand
  MainViewModel *-- MenuEditorViewModel
  MainViewModel ..> MenuRow
  MainViewModel ..> OrderRow
  MainViewModel ..> StatusChoice
  MainWindow ..> MainViewModel
  MainWindow ..> DialogService
  App ..> MainWindow
  App ..> MainViewModel
  Program ..> App
```

## Додавання позиції та сума замовлення зі знижкою

```mermaid
flowchart TD
  A([Початок]) --> B[/Позиція меню й кількість/]
  B --> C{Статус New і позиція доступна?}
  C -- ні --> E[/Виняток, дані не змінено/]
  C -- так --> D{Комбо?}
  D -- так --> F{Усі складники доступні?}
  F -- ні --> E
  F -- так --> G[Price × коефіцієнт знижки, округлення до копійок]
  D -- ні --> H[Ціна порції = Price]
  G --> I[Створити OrderLine зі знімком ціни]
  H --> I
  I --> J{Кількість 1–50 і значення коректні?}
  J -- ні --> E
  J -- так --> K[Сума рядка = UnitPrice × Quantity]
  K --> L{Сума замовлення без переповнення?}
  L -- ні --> E
  L -- так --> M[Додати рядок; Total = сума всіх рядків]
  M --> N[/Повернути суму/]
  N --> O([Кінець])
  E --> O
```

## Паралельний звіт продажів

```mermaid
flowchart TD
  A([Початок]) --> B[/Знімок рядків оплачених замовлень/]
  B --> C[Task.Run: створити спільний словник і lock]
  C --> D[Поділити масив на діапазони]
  D --> E[Parallel.ForEach: локальний словник кожної частини]
  E --> F{Скасування?}
  F -- так --> X[/Виняток скасування, результат не публікувати/]
  F -- ні --> G[Додати кількість і суму рядків за категоріями]
  G --> H{Є наступне замовлення у частині?}
  H -- так --> F
  H -- ні --> I[Під lock об'єднати локальні підсумки]
  I --> J[/Повідомити IProgress/]
  J --> K[Дочекатися завершення всіх частин]
  K --> L{Скасування?}
  L -- так --> X
  L -- ні --> M[Упорядкувати категорії, обчислити загальний виторг]
  M --> N[/Повернути SalesReport у ViewModel/]
  N --> O([Кінець])
  X --> O
```

Блоки від E до J виконуються для кожного діапазону паралельно. Відображення прогресу здійснюється через Dispatcher.UIThread.
