# Foundational Architectural Principles for .NET Development

이 문서들은 소프트웨어의 **아키텍처 원칙**에 대한 개요를 제공하며, 이는 **유지보수성**이 뛰어난 애플리케이션을 구축하는 데 필수적인 지침입니다. 여기서 다루는 주요 설계 원칙에는 소프트웨어의 기능을 분리하는 **관심사 분리**와 내부 구현을 외부로부터 보호하는 **캡슐화**가 포함됩니다. 또한, **의존성 역전 원칙**과 **명시적 의존성 원칙**은 느슨하게 결합된 시스템을 만드는 데 중요하며, 객체 지향 설계를 위한 **단일 책임 원칙**도 설명됩니다. 끝으로, **반복 금지(DRY)** 원칙, 특정 데이터 지속성 기술에 얽매이지 않는 **지속성 무지**, 그리고 복잡성을 관리하기 위한 **경계 컨텍스트** 개념이 강조됩니다.  

제시하신 자료(Architectural principles)를 문장 단위로 끊어 영어 원문과 한국어 번역을 제공하여 학습에 도움을 드리겠습니다.

---

### Architectural principles (아키텍처 원칙)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| Skip to main content Skip to Ask Learn chat experience This browser is no longer supported. | 주요 콘텐츠로 건너뛰기 Ask Learn 채팅 경험으로 건너뛰기 이 브라우저는 더 이상 지원되지 않습니다. | |
| Upgrade to Microsoft Edge to take advantage of the latest features, security updates, and technical support. | 최신 기능, 보안 업데이트 및 기술 지원을 활용하려면 Microsoft Edge로 업그레이드하십시오. | |
| Access to this page requires authorization. You can try signing in or changing directories. | 이 페이지에 액세스하려면 권한이 필요합니다. 로그인하거나 디렉터리를 변경해 볼 수 있습니다. | |
| Tip This content is an excerpt from the eBook, Architect Modern Web Applications with ASP.NET Core and Azure, available on .NET Docs or as a free downloadable PDF that can be read offline. | **팁:** 이 콘텐츠는 .NET 문서 또는 오프라인에서 읽을 수 있는 무료 다운로드 가능 PDF로 제공되는 전자책, *Architect Modern Web Applications with ASP.NET Core and Azure*에서 발췌한 내용입니다. | |
| "If builders built buildings the way programmers wrote programs, then the first woodpecker that came along would destroy civilization." *- Gerald Weinberg* | "만약 건축가들이 프로그래머들이 프로그램을 작성하는 방식대로 건물을 지었다면, 제일 먼저 나타난 딱따구리가 문명을 파괴했을 것이다." *- Gerald Weinberg* | |
| You should architect and design software solutions with **maintainability** in mind. | 여러분은 **유지보수성(maintainability)** 을 염두에 두고 소프트웨어 솔루션을 아키텍처하고 설계해야 합니다. | |
| The principles outlined in this section can help guide you toward architectural decisions that will result in clean, maintainable applications. | 이 섹션에 설명된 원칙들은 깔끔하고 유지보수가 용이한 애플리케이션을 만들게 될 아키텍처 결정으로 여러분을 안내하는 데 도움을 줄 수 있습니다. | |
| Generally, these principles will guide you toward building applications out of **discrete components** that are not tightly coupled to other parts of your application, but rather communicate through **explicit interfaces** or messaging systems. | 일반적으로, 이러한 원칙들은 애플리케이션의 다른 부분들과 강하게 결합되지 않고(not tightly coupled), 명시적인 인터페이스나 메시징 시스템을 통해 통신하는 **개별 구성 요소(discrete components)** 들로 애플리케이션을 구축하도록 안내할 것입니다. | |

---

### Common design principles (일반적인 설계 원칙)

#### Separation of concerns (관심사 분리)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| A guiding principle when developing is **Separation of Concerns**. | 개발 시 지침이 되는 원칙은 바로 **관심사 분리(Separation of Concerns)** 입니다. | |
| This principle asserts that software should be separated based on the kinds of work it performs. | 이 원칙은 소프트웨어가 수행하는 작업의 종류에 따라 분리되어야 한다고 주장합니다. | |
| For instance, consider an application that includes logic for identifying noteworthy items to display to the user, and which formats such items in a particular way to make them more noticeable. | 예를 들어, 사용자에게 표시할 주목할 만한 항목(noteworthy items)을 식별하는 로직과, 해당 항목들을 더 눈에 띄게 만들기 위해 특정 방식으로 포맷하는 로직을 포함하는 애플리케이션을 생각해 봅시다. | |
| The behavior responsible for choosing which items to format should be kept separate from the behavior responsible for formatting the items, since these behaviors are separate concerns that are only coincidentally related to one another. | 어떤 항목을 포맷할지 선택하는 역할을 하는 동작은 항목을 포맷하는 역할을 하는 동작과 분리되어 유지되어야 합니다. 왜냐하면 이 두 동작은 단지 우연히만 서로 관련이 있을 뿐, 별개의 관심사(separate concerns)이기 때문입니다. | |
| Architecturally, applications can be logically built to follow this principle by separating core business behavior from infrastructure and user-interface logic. | 아키텍처적으로, 애플리케이션은 핵심 비즈니스 동작을 인프라 및 사용자 인터페이스 로직과 분리함으로써 이 원칙을 따르도록 논리적으로 구축될 수 있습니다. | |
| Ideally, business rules and logic should reside in a separate project, which should not depend on other projects in the application. | 이상적으로, 비즈니스 규칙과 로직은 별도의 프로젝트에 상주해야 하며, 이 프로젝트는 애플리케이션의 다른 프로젝트에 의존해서는 안 됩니다. | |
| This separation helps ensure that the business model is easy to test and can evolve without being tightly coupled to **low-level implementation details** (it also helps if infrastructure concerns depend on abstractions defined in the business layer). | 이러한 분리는 비즈니스 모델이 테스트하기 쉽고, **하위 수준 구현 세부 사항(low-level implementation details)** 에 강하게 결합되지 않은 채 발전할 수 있도록 보장하는 데 도움이 됩니다 (또한 인프라 관심사가 비즈니스 계층에 정의된 추상화에 의존하는 경우에도 도움이 됩니다). | |
| Separation of concerns is a key consideration behind the use of **layers** in application architectures. | 관심사 분리는 애플리케이션 아키텍처에서 **계층(layers)** 을 사용하는 배경에 있는 핵심 고려 사항입니다. | |

#### Encapsulation (캡슐화)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| Different parts of an application should use **encapsulation** to **insulate** them from other parts of the application. | 애플리케이션의 서로 다른 부분들은 애플리케이션의 다른 부분들로부터 자신을 **격리시키기(insulate)** 위해 **캡슐화**를 사용해야 합니다. | |
| Application components and layers should be able to adjust their internal implementation without breaking their collaborators as long as **external contracts** are not violated. | 애플리케이션 구성 요소와 계층은 **외부 계약(external contracts)** 이 위반되지 않는 한, 협력자(collaborators)를 손상시키지 않고 내부 구현을 조정할 수 있어야 합니다. | |
| Proper use of encapsulation helps achieve **loose coupling** and **modularity** in application designs, since objects and packages can be replaced with **alternative implementations** so long as the same interface is maintained. | 캡슐화를 적절하게 사용하면 애플리케이션 설계에서 **느슨한 결합(loose coupling)** 과 **모듈성(modularity)** 을 달성하는 데 도움이 됩니다. 왜냐하면 동일한 인터페이스가 유지되는 한, 객체와 패키지를 **대체 구현(alternative implementations)** 으로 교체할 수 있기 때문입니다. | |
| In classes, encapsulation is achieved by limiting outside access to the class's **internal state**. | 클래스에서 캡슐화는 클래스의 **내부 상태(internal state)** 에 대한 외부 접근을 제한함으로써 달성됩니다. | |
| If an outside actor wants to manipulate the state of the object, it should do so through a well-defined function (or property setter), rather than having direct access to the private state of the object. | 외부 행위자(outside actor)가 객체의 상태를 조작하기를 원한다면, 객체의 private 상태에 직접 접근하는 대신, 잘 정의된 함수(또는 속성 설정자, property setter)를 통해 그렇게 해야 합니다. | |
| **Mutable global state** is **antithetical** to encapsulation. | **가변 전역 상태(Mutable global state)** 는 캡슐화와 **상반됩니다(antithetical)**. | |
| A value fetched from mutable global state in one function cannot be relied upon to have the same value in another function (or even further in the same function). | 한 함수에서 가변 전역 상태로부터 가져온 값은 다른 함수에서 (심지어 같은 함수 내에서도 더 나아가서) 동일한 값을 가질 것이라고 신뢰할 수 없습니다. | |
| A key consideration in **domain-driven design** and **clean architecture** is how to encapsulate access to data, and how to ensure application state is not made invalid by direct access to its persistence format. | **도메인 중심 설계(domain-driven design)** 및 **클린 아키텍처(clean architecture)** 의 주요 고려 사항은 데이터 접근을 캡슐화하는 방법과, 영속성 포맷(persistence format)에 대한 직접 접근으로 인해 애플리케이션 상태가 무효화되지 않도록 보장하는 방법입니다. | |

#### Dependency inversion (의존성 역전)

![Dependency Inversion](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/media/image4-1.png)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| The direction of dependency within the application should be in the direction of **abstraction**, not implementation details. | 애플리케이션 내 의존성의 방향은 구현 세부 사항이 아니라 **추상화(abstraction)** 의 방향이어야 합니다. | |
| Most applications are written such that **compile-time dependency** flows in the direction of **runtime execution**, producing a **direct dependency graph**. | 대부분의 애플리케이션은 **컴파일 시간 의존성(compile-time dependency)** 이 **런타임 실행(runtime execution)** 방향으로 흐르도록 작성되어 **직접적인 의존성 그래프(direct dependency graph)** 를 생성합니다. | |
| Applying the **dependency inversion principle** allows A to call methods on an abstraction that B implements, making it possible for A to call B at run time, but for B to depend on an interface controlled by A at compile time (thus, *inverting* the typical compile-time dependency). | **의존성 역전 원칙(dependency inversion principle)** 을 적용하면 A가 B가 구현하는 추상화의 메서드를 호출할 수 있게 됩니다. 이를 통해 A가 런타임에 B를 호출하는 것이 가능해지지만, B는 컴파일 시점에 A가 제어하는 인터페이스에 의존하게 됩니다 (따라서 일반적인 컴파일 시간 의존성을 **역전(inverting)** 시킵니다). | |
| **Dependency inversion** is a key part of building **loosely coupled** applications, since implementation details can be written to depend on and implement **higher-level abstractions**, rather than the other way around. | **의존성 역전**은 **느슨하게 결합된(loosely coupled)** 애플리케이션을 구축하는 핵심 부분입니다. 왜냐하면 구현 세부 사항이 그 반대가 아니라 **상위 수준 추상화(higher-level abstractions)** 에 의존하고 이를 구현하도록 작성될 수 있기 때문입니다. | |
| The resulting applications are more **testable**, **modular**, and **maintainable** as a result. | 그 결과로 생성된 애플리케이션은 더 **테스트하기 쉽고(testable)**, **모듈화되며(modular)**, **유지보수가 용이**해집니다. | |
| The practice of ***dependency injection*** is made possible by following the dependency inversion principle. | *의존성 주입(dependency injection)* 의 관행은 의존성 역전 원칙을 따름으로써 가능해집니다. | |

![](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/media/image4-2.png)  

#### Explicit dependencies (명시적 의존성)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| **Methods and classes should explicitly require any collaborating objects they need in order to function correctly.** | **메서드와 클래스는 올바르게 기능하기 위해 필요한 모든 협력 객체(collaborating objects)를 명시적으로 요구해야 합니다.** | |
| It is called the **Explicit Dependencies Principle**. | 이를 **명시적 의존성 원칙(Explicit Dependencies Principle)** 이라고 합니다. | |
| Class **constructors** provide an opportunity for classes to identify the things they need in order to be in a valid state and to function properly. | 클래스 **생성자(Class constructors)** 는 클래스가 유효한 상태에 있고 적절하게 기능하기 위해 필요한 것들을 식별할 기회를 제공합니다. | |
| If you define classes that can be constructed and called, but that will only function properly if certain global or infrastructure components are in place, these classes are being ***dishonest*** with their clients. | 구성 및 호출은 가능하지만, 특정 전역 또는 인프라 구성 요소가 제자리에 있을 때만 제대로 기능하는 클래스를 정의한다면, 이러한 클래스는 클라이언트에게 ***불성실한(dishonest)*** 것입니다. | |
| By following the explicit dependencies principle, your classes and methods are being **honest** with their clients about what they need in order to function. | 명시적 의존성 원칙을 따름으로써, 여러분의 클래스와 메서드는 기능하기 위해 무엇이 필요한지에 대해 클라이언트에게 **정직해집니다**. | |
| Following the principle makes your code more **self-documenting** and your **coding contracts** more **user-friendly**. | 이 원칙을 따르면 코드가 더 **자가 설명적(self-documenting)** 이 되고 **코딩 계약(coding contracts)** 이 더 **사용자 친화적(user-friendly)** 이 됩니다. | |

#### Single responsibility (단일 책임)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| The **single responsibility principle** applies to object-oriented design, but can also be considered as an architectural principle similar to separation of concerns. | **단일 책임 원칙(single responsibility principle)** 은 객체 지향 설계에 적용되지만, 관심사 분리와 유사한 아키텍처 원칙으로도 간주될 수 있습니다. | |
| It states that objects should have only one responsibility and that they should have only **one reason to change**. | 이 원칙은 객체가 오직 하나의 책임만 가져야 하며, 변경될 이유도 오직 **하나**여야 한다고 명시합니다. | |
| Specifically, the only situation in which the object should change is if the manner in which it performs its one responsibility must be updated. | 구체적으로, 객체가 변경되어야 하는 유일한 상황은 그것이 수행하는 단일 책임의 방식이 업데이트되어야 하는 경우입니다. | |
| In a **monolithic application**, we can apply the single responsibility principle at a high level to the layers in the application. | **모놀리식 애플리케이션(monolithic application)** 에서 우리는 애플리케이션의 계층에 높은 수준으로 단일 책임 원칙을 적용할 수 있습니다. | |
| Presentation responsibility should remain in the UI project, while data access responsibility should be kept within an infrastructure project. | 프리젠테이션 책임(Presentation responsibility)은 UI 프로젝트에 남아 있어야 하며, 데이터 접근 책임(data access responsibility)은 인프라 프로젝트 내에 유지되어야 합니다. | |
| When this principle is applied to application architecture and taken to its logical endpoint, you get **microservices**. | 이 원칙이 애플리케이션 아키텍처에 적용되고 그 논리적 종착점까지 가면, **마이크로서비스(microservices)** 를 얻게 됩니다. | |
| A given microservice should have a **single responsibility**. | 주어진 마이크로서비스는 **단일 책임**을 가져야 합니다. | |
| If you need to extend the behavior of a system, it's usually better to do it by adding additional microservices, rather than by adding responsibility to an existing one. | 시스템의 동작을 확장해야 하는 경우, 일반적으로 기존 마이크로서비스에 책임을 추가하는 대신 추가적인 마이크로서비스를 추가하여 수행하는 것이 더 좋습니다. | |

#### Don't repeat yourself (DRY) (반복하지 마라)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| The application should avoid specifying behavior related to a particular concept in **multiple places** as this practice is a frequent source of errors. | 애플리케이션은 특정 개념과 관련된 동작을 **여러 곳에서(multiple places)** 명시하는 것을 피해야 합니다. 왜냐하면 이러한 관행은 오류의 빈번한 원천이기 때문입니다. | |
| At some point, a change in requirements will require changing this behavior. | 어떤 시점에서 요구 사항의 변경은 이 동작을 변경하도록 요구할 것입니다. | |
| It's likely that at least one instance of the behavior will fail to be updated, and the system will behave **inconsistently**. | 그 동작의 인스턴스 중 적어도 하나는 업데이트되지 못할 가능성이 높으며, 시스템은 **일관성 없이(inconsistently)** 동작할 것입니다. | |
| Rather than duplicating logic, **encapsulate** it in a programming construct. | 로직을 중복하는 대신, 프로그래밍 구성체(programming construct) 내에 **캡슐화**하십시오. | |
| **Duplication is always preferable to coupling to the wrong abstraction.** | **잘못된 추상화에 결합(coupling)하는 것보다는 중복(Duplication)이 항상 더 낫습니다.** | |

#### Persistence ignorance (영속성 무시)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| **Persistence ignorance (PI)** refers to types that need to be persisted, but whose code is unaffected by the choice of persistence technology. | **영속성 무시(Persistence ignorance, PI)** 란 영속화(persisted)되어야 하지만, 그 코드가 영속성 기술의 선택에 의해 영향을 받지 않는 타입들을 의미합니다. | |
| Such types in .NET are sometimes referred to as **Plain Old CLR Objects (POCOs)**, because they do not need to inherit from a particular base class or implement a particular interface. | .NET에서 이러한 타입들은 특정 기본 클래스를 상속하거나 특정 인터페이스를 구현할 필요가 없기 때문에 때로는 **POCO(Plain Old CLR Objects)** 로 불립니다. | |
| Persistence ignorance is valuable because it allows the same business model to be persisted in multiple ways, offering **additional flexibility** to the application. | 영속성 무시는 동일한 비즈니스 모델을 여러 방식으로 영속화할 수 있게 하여 애플리케이션에 **추가적인 유연성(additional flexibility)** 을 제공하기 때문에 중요합니다. | |
| The requirement that classes have any of the above features or behaviors adds coupling between the types to be persisted and the choice of persistence technology, making it more difficult to adopt new data access strategies in the future. | 클래스가 (필수 기본 클래스, 필수 인터페이스 구현 등) 위의 기능이나 동작 중 하나라도 가져야 한다는 요구 사항은 영속화될 타입과 영속성 기술 선택 간의 결합을 추가하여, 미래에 새로운 데이터 접근 전략을 채택하는 것을 더 어렵게 만듭니다. | |

#### Bounded contexts (바운디드 컨텍스트)

| 영어 원문 (English Source) | 한국어 번역 (Korean Translation) | 출처 |
| :--- | :--- | :--- |
| **Bounded contexts** are a central pattern in **Domain-Driven Design**. | **바운디드 컨텍스트(Bounded contexts)** 는 **도메인 중심 설계(Domain-Driven Design)** 의 핵심 패턴입니다. | |
| They provide a way of tackling complexity in large applications or organizations by breaking it up into **separate conceptual modules**. | 이는 복잡성을 **별개의 개념적 모듈(separate conceptual modules)** 로 나누어 대규모 애플리케이션 또는 조직의 복잡성을 해결하는 방법을 제공합니다. | |
| Each conceptual module then represents a context that is separated from other contexts (**hence, bounded**), and can **evolve independently**. | 그런 다음 각 개념적 모듈은 다른 컨텍스트로부터 분리된 (**따라서, 바운디드된**) 컨텍스트를 나타내며 **독립적으로 발전할(evolve independently)** 수 있습니다. | |
| At a minimum, individual web applications should strive to be their own bounded context, with their own persistence store for their business model, rather than **sharing a database** with other applications. | 최소한 개별 웹 애플리케이션은 다른 애플리케이션과 **데이터베이스를 공유하기보다는**, 자체 비즈니스 모델을 위한 자체 영속성 저장소(persistence store)를 가진 자체 바운디드 컨텍스트가 되도록 노력해야 합니다. | |
| Communication between bounded contexts occurs through **programmatic interfaces**, rather than through a shared database. | 바운디드 컨텍스트 간의 통신은 공유 데이터베이스를 통하는 대신 **프로그래밍 인터페이스(programmatic interfaces)** 를 통해 발생합니다. | |
| Bounded contexts map closely to **microservices**, which also are ideally implemented as their own individual bounded contexts. | 바운디드 컨텍스트는 **마이크로서비스**와 밀접하게 연결되며, 마이크로서비스 역시 이상적으로는 자체적인 개별 바운디드 컨텍스트로 구현됩니다. | |

요청하신 의존성 역전 원칙(Dependency Inversion Principle, DIP)에 대한 .NET 코드 예제를 제공하고자 합니다.

먼저, 제공된 자료(–)를 확인한 결과, **의존성 역전 원칙**의 중요성, 개념, 그리고 이것이 **의존성 주입(Dependency Injection)** 을 가능하게 한다는 설명은 포함되어 있지만, 이 원칙을 직접 시연하는 **실제 C#/.NET 코드 예제는 포함되어 있지 않습니다.**

제공된 소스는 의존성 역전 원칙에 대해 다음과 같이 설명합니다.

*   애플리케이션 내 의존성의 방향은 구현 세부 사항이 아니라 **추상화(abstraction)** 의 방향이어야 합니다.
*   일반적인 애플리케이션에서는 컴파일 시간 의존성이 런타임 실행 방향으로 흐르지만, DIP를 적용하면 상위 수준 모듈(A)이 하위 수준 모듈(B)이 구현하는 추상화(인터페이스)를 호출하게 됩니다.
*   이를 통해 컴파일 시 의존성을 *역전*시킵니다. 그 결과, 애플리케이션은 더 **테스트하기 쉽고(testable), 모듈화되며(modular), 유지보수가 용이**해집니다.

따라서, 이 원칙을 이해하는 데 도움을 드리기 위해, **외부 정보**를 사용하여 DIP를 구현하는 .NET 코드 예제를 아래에 제시합니다. 이 코드는 제공된 소스에 기반한 것이 아니므로, 참고 시 독립적인 검증이 필요할 수 있습니다.

---

### 의존성 역전 원칙 (.NET/C# 예제)

의존성 역전 원칙은 두 가지 핵심 요소로 구현됩니다.
1.  상위 수준 모듈(예: `BusinessLogic`)은 하위 수준 모듈(예: `DataAccess`)의 구체적인 구현 클래스에 의존해서는 안 됩니다.
2.  두 모듈 모두 **추상화(인터페이스)** 에 의존해야 합니다.

#### 1단계: 추상화 (Abstraction) 정의 (상위/하위 모듈 모두 여기에 의존)

상위 수준 모듈이 필요로 하는 계약(Contract)을 정의합니다.

```csharp
// 1. 추상화 (인터페이스): 데이터 저장 기능을 정의합니다.
// 이 인터페이스는 비즈니스 계층(상위 모듈)에 의해 정의됩니다.
public interface IDataStore
{
    void SaveData(string data);
}
```

#### 2단계: 하위 수준 모듈 (Low-Level Module)

실제 데이터 저장소(예: SQL 데이터베이스)와 통신하는 구체적인 구현입니다. 이 모듈은 상위 수준 모듈이 정의한 **`IDataStore` 인터페이스에 의존**하고 이를 구현합니다.

```csharp
// 2. 하위 수준 구현: 파일 시스템 접근이라는 구현 세부 사항입니다.
// 이 클래스는 IDataStore 인터페이스에 의존합니다 (역전).
public class FileSystemDataStore : IDataStore
{
    public void SaveData(string data)
    {
        Console.WriteLine($"[하위 모듈] 데이터를 파일 시스템에 저장: {data}");
        // 실제 파일 저장 로직이 여기에 들어갑니다.
    }
}

// 만약 SQL DB를 사용하고 싶다면, 이 클래스를 추가하고 IDataStore를 구현합니다.
public class SqlDataStore : IDataStore
{
    public void SaveData(string data)
    {
        Console.WriteLine($"[하위 모듈] 데이터를 SQL 데이터베이스에 저장: {data}");
        // 실제 DB 저장 로직이 여기에 들어갑니다.
    }
}
```

#### 3단계: 상위 수준 모듈 (High-Level Module)

핵심 비즈니스 로직을 포함하는 모듈입니다. 이 모듈은 구체적인 `FileSystemDataStore` 클래스 대신 **`IDataStore` 인터페이스에만 의존**합니다. 이는 DIP의 핵심 원칙인 **명시적 의존성 원칙** (생성자를 통해 필요한 객체를 명시적으로 요구)을 따르며, 의존성 주입(DI)을 통해 이루어집니다.

```csharp
// 3. 상위 수준 모듈: 비즈니스 로직을 담당합니다.
public class BusinessLogicProcessor
{
    // 구체적인 구현(FileSystemDataStore)이 아닌 추상화(IDataStore)에 의존합니다.
    private readonly IDataStore _dataStore;

    // 생성자를 통한 의존성 주입 (Dependency Injection)
    public BusinessLogicProcessor(IDataStore dataStore)
    {
        // 명시적 의존성 원칙을 따릅니다.
        _dataStore = dataStore;
    }

    public void ProcessAndSave(string input)
    {
        string processedData = $"[상위 모듈] 처리된 데이터: {input.ToUpper()}";
        
        // 데이터 저장 로직은 IDataStore 인터페이스를 통해 호출됩니다.
        _dataStore.SaveData(processedData);
    }
}
```

#### DIP를 통한 실행

애플리케이션의 시작 부분(Composition Root, ASP.NET Core에서는 보통 DI 컨테이너)에서 의존성을 주입하여 실행합니다.

```csharp
public class Program
{
    public static void Main()
    {
        // 런타임에 어떤 구현체를 사용할지 결정합니다.

        // 1. 파일 시스템 구현 사용 (첫 번째 시나리오)
        IDataStore fileDataStore = new FileSystemDataStore();
        BusinessLogicProcessor processor1 = new BusinessLogicProcessor(fileDataStore);
        processor1.ProcessAndSave("test data for file");
        // 출력: [상위 모듈] 처리된 데이터: TEST DATA FOR FILE
        //       [하위 모듈] 데이터를 파일 시스템에 저장: [상위 모듈] 처리된 데이터: TEST DATA FOR FILE

        Console.WriteLine("\n--- 구현 변경 ---\n");

        // 2. SQL 데이터베이스 구현 사용 (구현 세부 사항만 변경)
        // 상위 모듈(BusinessLogicProcessor)은 코드 변경 없이 다른 하위 모듈을 사용할 수 있습니다.
        IDataStore sqlDataStore = new SqlDataStore();
        BusinessLogicProcessor processor2 = new BusinessLogicProcessor(sqlDataStore);
        processor2.ProcessAndSave("test data for sql");
        // 출력: [상위 모듈] 처리된 데이터: TEST DATA FOR SQL
        //       [하위 모듈] 데이터를 SQL 데이터베이스에 저장: [상위 모듈] 처리된 데이터: TEST DATA FOR SQL
    }
}
```

# reference 

- [Architecture](https://learn.microsoft.com/ko-kr/dotnet/architecture/modern-web-apps-azure/architectural-principles)  
- [DI](https://learn.microsoft.com/ko-kr/dotnet/core/extensions/dependency-injection)  