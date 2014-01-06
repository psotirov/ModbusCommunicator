-- Table creation into the test database
CREATE  TABLE `test`.`measurements` (

  `id` INT NOT NULL AUTO_INCREMENT ,

  `timestamp` DATETIME NOT NULL ,

  `co2` FLOAT NOT NULL ,

  `o2` FLOAT NOT NULL ,

  PRIMARY KEY (`id`) ,

  UNIQUE INDEX `id_UNIQUE` (`id` ASC) ,

  UNIQUE INDEX `timestamp_UNIQUE` (`timestamp` ASC) );

-- connection string
-- Server=localhost;Database=test;Uid=root;Pwd=;