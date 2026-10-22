-- =====================================================================
-- FitZone Training (FZT) - Fitness Class Scheduling & Booking System
-- database_creation_queries.sql : creates the database, tables, constraints, and views
-- Run this first, then data_loading_queries.sql
-- =====================================================================

DROP DATABASE IF EXISTS fitzone;
CREATE DATABASE fitzone CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE fitzone;

-- ---------------------------------------------------------------------
-- USER (supertype) : every person who can log in
-- ---------------------------------------------------------------------
CREATE TABLE app_user (
    user_id        INT AUTO_INCREMENT PRIMARY KEY,
    first_name     VARCHAR(50)  NOT NULL,
    last_name      VARCHAR(50)  NOT NULL,
    email          VARCHAR(100) NOT NULL UNIQUE,
    phone          VARCHAR(20),
    password_hash  VARCHAR(255) NOT NULL,
    role           ENUM('customer','trainer') NOT NULL,
    created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ---------------------------------------------------------------------
-- CUSTOMER (subtype of USER)
-- ---------------------------------------------------------------------
CREATE TABLE customer (
    customer_id        INT PRIMARY KEY,
    date_of_birth      DATE,
    fitness_goal       VARCHAR(200),
    emergency_contact  VARCHAR(100),
    CONSTRAINT fk_customer_user FOREIGN KEY (customer_id)
        REFERENCES app_user(user_id) ON DELETE CASCADE
);

-- ---------------------------------------------------------------------
-- TRAINER (subtype of USER)
-- ---------------------------------------------------------------------
CREATE TABLE trainer (
    trainer_id        INT PRIMARY KEY,
    bio               VARCHAR(1000),
    specialty         VARCHAR(100),
    years_experience  TINYINT UNSIGNED DEFAULT 0,
    certifications    VARCHAR(255),
    CONSTRAINT fk_trainer_user FOREIGN KEY (trainer_id)
        REFERENCES app_user(user_id) ON DELETE CASCADE
);

-- ---------------------------------------------------------------------
-- AUTH_SESSION : login tokens (one user -> many tokens)
-- ---------------------------------------------------------------------
CREATE TABLE auth_session (
    token       CHAR(64) PRIMARY KEY,
    user_id     INT NOT NULL,
    created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at  DATETIME NOT NULL,
    CONSTRAINT fk_auth_user FOREIGN KEY (user_id)
        REFERENCES app_user(user_id) ON DELETE CASCADE
);

-- ---------------------------------------------------------------------
-- CATEGORY : type of workout (Yoga, HIIT, Strength, ...)
-- ---------------------------------------------------------------------
CREATE TABLE category (
    category_id  INT AUTO_INCREMENT PRIMARY KEY,
    name         VARCHAR(50) NOT NULL UNIQUE,
    description  VARCHAR(255)
);

-- ---------------------------------------------------------------------
-- ROOM : physical location in the gym where sessions take place
-- ---------------------------------------------------------------------
CREATE TABLE room (
    room_id   INT AUTO_INCREMENT PRIMARY KEY,
    name      VARCHAR(50) NOT NULL UNIQUE,
    capacity  SMALLINT UNSIGNED NOT NULL
);

-- ---------------------------------------------------------------------
-- FITNESS_CLASS : a class/offering created and owned by a trainer.
-- class_type = 'group'   -> many customers per session
-- class_type = 'private' -> 1-on-1 personal training (max_capacity = 1)
-- ---------------------------------------------------------------------
CREATE TABLE fitness_class (
    class_id       INT AUTO_INCREMENT PRIMARY KEY,
    trainer_id     INT NOT NULL,
    category_id    INT NOT NULL,
    title          VARCHAR(100) NOT NULL,
    description    VARCHAR(1000),
    class_type     ENUM('group','private') NOT NULL DEFAULT 'group',
    difficulty     ENUM('Beginner','Intermediate','Advanced') NOT NULL DEFAULT 'Beginner',
    duration_min   SMALLINT UNSIGNED NOT NULL,
    price          DECIMAL(8,2) NOT NULL DEFAULT 0.00,
    max_capacity   SMALLINT UNSIGNED NOT NULL,
    is_active      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_class_trainer  FOREIGN KEY (trainer_id)  REFERENCES trainer(trainer_id),
    CONSTRAINT fk_class_category FOREIGN KEY (category_id) REFERENCES category(category_id),
    CONSTRAINT chk_class_capacity CHECK (max_capacity >= 1),
    CONSTRAINT chk_class_price    CHECK (price >= 0),
    CONSTRAINT chk_private_cap    CHECK (class_type = 'group' OR max_capacity = 1)
);

-- ---------------------------------------------------------------------
-- CLASS_SESSION : one scheduled occurrence (date/time) of a class
-- ---------------------------------------------------------------------
CREATE TABLE class_session (
    session_id  INT AUTO_INCREMENT PRIMARY KEY,
    class_id    INT NOT NULL,
    room_id     INT,
    start_time  DATETIME NOT NULL,
    end_time    DATETIME NOT NULL,
    status      ENUM('scheduled','cancelled','completed') NOT NULL DEFAULT 'scheduled',
    CONSTRAINT fk_session_class FOREIGN KEY (class_id)
        REFERENCES fitness_class(class_id) ON DELETE CASCADE,
    CONSTRAINT fk_session_room  FOREIGN KEY (room_id)
        REFERENCES room(room_id) ON DELETE SET NULL,
    CONSTRAINT chk_session_time CHECK (end_time > start_time),
    INDEX idx_session_start (start_time)
);

-- ---------------------------------------------------------------------
-- BOOKING : associative entity, CUSTOMER  M:N  CLASS_SESSION
-- ---------------------------------------------------------------------
CREATE TABLE booking (
    booking_id         INT AUTO_INCREMENT PRIMARY KEY,
    customer_id        INT NOT NULL,
    session_id         INT NOT NULL,
    confirmation_code  CHAR(8) NOT NULL UNIQUE,
    status             ENUM('confirmed','cancelled','attended','no_show') NOT NULL DEFAULT 'confirmed',
    booked_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    cancelled_at       DATETIME,
    notes              VARCHAR(255),
    CONSTRAINT fk_booking_customer FOREIGN KEY (customer_id)
        REFERENCES customer(customer_id) ON DELETE CASCADE,
    CONSTRAINT fk_booking_session  FOREIGN KEY (session_id)
        REFERENCES class_session(session_id) ON DELETE CASCADE,
    CONSTRAINT uq_booking_customer_session UNIQUE (customer_id, session_id)
);

-- ---------------------------------------------------------------------
-- VIEW : sessions with live seat availability (used by search page)
-- ---------------------------------------------------------------------
CREATE VIEW v_session_availability AS
SELECT  s.session_id,
        s.class_id,
        s.start_time,
        s.end_time,
        s.status,
        r.name                           AS room_name,
        c.max_capacity,
        COUNT(b.booking_id)              AS booked_count,
        c.max_capacity - COUNT(b.booking_id) AS seats_left
FROM    class_session s
JOIN    fitness_class c ON c.class_id = s.class_id
LEFT JOIN room r        ON r.room_id  = s.room_id
LEFT JOIN booking b     ON b.session_id = s.session_id
                       AND b.status IN ('confirmed','attended')
GROUP BY s.session_id, s.class_id, s.start_time, s.end_time, s.status,
         r.name, c.max_capacity;
