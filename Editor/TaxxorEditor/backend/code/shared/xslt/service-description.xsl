<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">


    <xsl:param name="service-name">undefined</xsl:param>
    <xsl:param name="service-id">undefined</xsl:param>

    <!-- Homepage determines if we are dealing with a web-application (exposes HTML based ui) or with a service -->
    <xsl:variable name="application-type">
        <xsl:choose>
            <xsl:when test="/items/structured/item[@id = 'apiroot']">
                <xsl:text>service</xsl:text>
            </xsl:when>
            <xsl:otherwise>
                <xsl:text>webapplication</xsl:text>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:variable>

    <xsl:output method="xml" indent="yes"/>


    <xsl:template match="/">
        <application id="{$service-id}" type="{$application-type}">
            <meta>
                <name>
                    <xsl:value-of select="$service-name"/>
                </name>
                <details>
                    <!--
                    <injected_content>
                        <xsl:copy-of select="//repositories/*"/>
                    </injected_content>
                    -->
                    <repositories>
                        <xsl:apply-templates select="//repositories//repro"/>
                    </repositories>

                </details>
            </meta>
            <routes>
                <!-- Add the API routes -->
                <xsl:apply-templates select="/items/structured//item[@id='apiroot']/sub_items/item"/>
                <!-- Add the "unstructured" routes -->
                <xsl:apply-templates select="/items/unstructured//item"/>
            </routes>
        </application>
    </xsl:template>


    <xsl:template match="item">
        <route id="{@id}" uri="{web_page/path}">
            <name>
                <xsl:value-of select="web_page/linkname"/>
            </name>
            <xsl:apply-templates select="sub_items/item"/>
        </route>
    </xsl:template>
    
    <xsl:template match="sub_items">
        <xsl:apply-templates/>
    </xsl:template>


    <xsl:template match="repro">
        <repro id="{@id}" type="repro">
            <xsl:if test="location/@version | @version">
                <xsl:attribute name="version">
                    <xsl:value-of select="location/@version | @version"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="name | location/@name | @name">
                <xsl:attribute name="name">
                    <xsl:value-of select="name | location/@name | @name"/>
                </xsl:attribute>
            </xsl:if>
            <!-- Render the submodules -->
            <xsl:apply-templates select="submodules/location"/>
        </repro>
    </xsl:template>

    <xsl:template match="location">
        <repro id="{@id}" type="submodule">
            <xsl:if test="@version">
                <xsl:attribute name="version">
                    <xsl:value-of select="@version"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="name | location/@name | @name">
                <xsl:attribute name="name">
                    <xsl:value-of select="name | location/@name | @name"/>
                </xsl:attribute>
            </xsl:if>
        </repro>
    </xsl:template>

</xsl:stylesheet>
