# Stage 1: Build con Maven y Java 17 (Temurin)
# FROM maven:3.8.5-eclipse-temurin-17 AS build
FROM maven:3.8.4-openjdk-17 AS build
WORKDIR /app
# COPY pom.xml .
COPY frontend/src ./src
RUN mvn clean package -DskipTests


FROM eclipse-temurin:17-jre-alpine
WORKDIR /app

COPY --from=build /app/target/*.jar app.jar
EXPOSE 8080
ENTRYPOINT ["java", "-jar", "app.jar"]
